// Assets/Scripts/Data/Actions/ProcessDocumentCat2Executor.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Gameplay; // Подключаем пространство имен с OfficeObjectDurability
using Clinch;
using Enums;

public class ProcessDocumentCat2Executor : ActionExecutor
{
    public override bool IsInterruptible => false;
    
    protected override IEnumerator ActionRoutine()
    {
        yield return ReturnToWorkstation();
        if (!IsAtWorkstation) { FinishAction(false); yield break; }
        var clerk = staff as ClerkController;
        // Проверки
        if (clerk == null || clerk.assignedWorkstation == null) 
        { 
            FinishAction(false); 
            yield break; 
        }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        var client = zone?.GetOccupyingClients().FirstOrDefault(c => c != null && !c.isLeavingSuccessfully);
        
        // Бронебойный поиск физически ближайшего клиента у стола
        if (client == null && clerk.assignedWorkstation != null)
        {
            client = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c != null && !c.isLeavingSuccessfully &&
                Vector2.Distance(c.transform.position, clerk.assignedWorkstation.clientStandPoint.transform.position) < 1.5f);
        }

        if (client == null || !client.CanContinueService || client.docHolder == null)
        {
            FinishAction(false);
            yield break;
        }

        // --- КЛИНЧ-ПРОВЕРКА (НЕЗАВИСИМЫЕ КУБИКИ) ---
        var clinchConfig = Gameplay.AIBalanceConfig.Instance;
        Clinch.ClinchTarget clinchToTrigger = null;
        
        if (clinchConfig != null)
        {
            // Бросок для клиента
            bool rollClient = Random.value < (clinchConfig.clientClinchBaseChance * (1f + client.suetunFactor));
            // Бросок для персонала
            bool rollStaff = Random.value < (clinchConfig.staffClinchBaseChance * (1f - staff.skills.sedentaryResilience));
            
            Clinch.ClinchTarget clinch = null;
            
            if (rollClient)
            {
                clinch = client.GetComponent<Clinch.ClinchTarget>();
                if (clinch == null) clinch = client.gameObject.AddComponent<Clinch.ClinchTarget>();
            }
            else if (rollStaff)
            {
                clinch = staff.GetComponent<Clinch.ClinchTarget>();
                if (clinch == null) clinch = staff.gameObject.AddComponent<Clinch.ClinchTarget>();
            }

            if (clinch != null && !clinch.IsActive)
            {
                clinchToTrigger = clinch;
            }
            
            if (clinchToTrigger != null)
            {
                clinchToTrigger.TriggerClinch();
                staff.thoughtBubble?.ShowPriorityMessage("Эмм... Директор!", 2f, Color.yellow);
                yield return new WaitWhile(() => clinchToTrigger.IsActive);
                yield return new WaitForSeconds(2.5f); // Даём реакции проявиться
            }
        }
        // После WaitWhile ОБЯЗАТЕЛЬНАЯ ПРОВЕРКА для всех экзекуторов:
        if (client == null || !client.CanContinueService)
        {
            // Клиент обиделся и ушел по таймауту клинча
            if (staff is ClerkController c) c.SetState(ClerkController.ClerkState.Working);
            FinishAction(false);
            yield break;
        }
        // --- КЛИНЧ-ПРОВЕРКА (КОНЕЦ) ---

        // --- ИНТЕГРАЦИЯ ИЗНОСА (НАЧАЛО) ---
        // 1. Получаем компонент прочности
        var durability = clerk.assignedWorkstation.GetComponent<OfficeObjectDurability>();
        
        // 2. Проверка на поломку
        if (durability != null && !durability.IsUsable())
        {
            clerk.thoughtBubble?.ShowPriorityMessage("Стол сломан!", 3f, Color.red);
            clerk.SetState(ClerkController.ClerkState.Working); 
            FinishAction(false);
            yield break;
        }

        // 3. Расчет эффективности
        float efficiency = Mathf.Max(.05f, durability != null ? durability.GetEfficiencyMultiplier() : 1.0f);
        // --- ИНТЕГРАЦИЯ ИЗНОСА (КОНЕЦ) ---

        clerk.SetState(ClerkController.ClerkState.Working);
        
        // 1. Проверяем, правильный ли бланк у клиента
        if (client.docHolder.GetCurrentDocumentType() != DocumentType.Form2) 
        {
            clerk.thoughtBubble?.ShowPriorityMessage("Это не тот бланк,\nвозьмите другой.", 3f, Color.yellow);
            client.ApplyStressJump(client.stressJump_Refusal);
            client.stateMachine.GoGetFormAndReturn();
            FinishAction(true); // Задача выполнена (клиент отправлен)
            yield break;
        }
        
        // 2. Проверяем документ на ошибки (если клерк умеет)
        bool canCheckDocuments = clerk.activeActions.Any(a => a.actionType == ActionType.CheckDocument);
        if (canCheckDocuments)
        {
            clerk.thoughtBubble?.ShowPriorityMessage("Проверяю...", 2f, Color.yellow);
            yield return new WaitForSeconds(Random.Range(1f, 3f));
            if (client == null || !client.CanContinueService) { FinishAction(false); yield break; }
            if (Random.value < (1f - client.documentQuality) && Random.value < clerk.skills.pedantry)
            {
                clerk.thoughtBubble?.ShowPriorityMessage("Здесь ошибка!\nНужно переделать.", 3f, Color.red);
                client.ApplyStressJump(client.stressJump_Refusal);
                yield return new WaitForSeconds(2f);
                if (client == null || !client.CanContinueService) { FinishAction(false); yield break; }
                client.stateMachine.GoGetFormAndReturn();
                FinishAction(true); // Задача выполнена (ошибка найдена)
                yield break;
            }
        }
        
        // --- 3. АНИМАЦИЯ: Забираем документ у клиента ---
        DocumentHolder clientDocHolder = client.docHolder;
        Transform clientHand = clientDocHolder?.handPoint;
        Transform deskPoint = clerk.assignedWorkstation.documentPointOnDesk;
        GameObject currentClientDocObject = null;
        GameObject flyingDoc = null;
        GameObject outgoingDocument = null;
        bool documentCommitted = false;
        bool documentBorrowed = false;
        var originalType = clientDocHolder.GetCurrentDocumentType();
        var originalData = clientDocHolder.GetCurrentDocumentData();
        RegisterCleanup(() =>
        {
            if (outgoingDocument != null)
            {
                outgoingDocument.GetComponent<DocumentMover>()?.CancelMove();
                if (!documentCommitted) Destroy(outgoingDocument);
            }
            if (currentClientDocObject != null)
                currentClientDocObject.GetComponent<DocumentMover>()?.CancelMove();
            if (documentBorrowed && !documentCommitted && clientDocHolder != null)
            {
                if (originalData != null) clientDocHolder.SetDocument(originalData);
                else clientDocHolder.SetDocument(originalType);
            }
            if (currentClientDocObject != null) Destroy(currentClientDocObject);
        });
        if (deskPoint != null)
        {
            currentClientDocObject = clientDocHolder.DetachDocument();
            documentBorrowed = true;
        }

        if (currentClientDocObject != null && deskPoint != null)
        {
            DocumentMover mover = currentClientDocObject.GetComponent<DocumentMover>() ?? currentClientDocObject.AddComponent<DocumentMover>();
            bool arrived = false;
            mover.StartMove(deskPoint, () => { arrived = true; });
            yield return new WaitUntil(() => arrived || currentClientDocObject == null || deskPoint == null);
            if (client == null || !client.CanContinueService || deskPoint == null) { FinishAction(false); yield break; }
            flyingDoc = currentClientDocObject; // Запоминаем документ на столе
            if (flyingDoc != null) 
            {
                 flyingDoc.transform.SetParent(deskPoint);
                 flyingDoc.transform.localPosition = Vector3.zero;
                 flyingDoc.transform.localRotation = Quaternion.identity;
            }
        }
        else {
             Debug.LogWarning($" -> Не удалось анимировать забор документа у {client.name}.");
        }
        // --- Конец анимации забора ---

        clerk.thoughtBubble?.ShowPriorityMessage("Обрабатываю (Кат. 2)...", 3f, Color.white);
        
        // --- ПРИМЕНЕНИЕ ЭФФЕКТИВНОСТИ ---
        float workTime = Random.Range(2f, 4f) / efficiency;

        // Пытаемся достать префаб через ссылки клерка
        var refs = clerk.GetComponent<StaffPrefabReferences>();
        if (refs != null && refs.processingIconPrefab != null)
        {
            GameObject iconObj = Instantiate(refs.processingIconPrefab);
            RegisterCleanup(() => { if (iconObj != null) Destroy(iconObj); });
            ProcessingAnimationUI animScript = iconObj.GetComponent<ProcessingAnimationUI>();
            if (animScript != null)
            {
                animScript.Play(clerk.transform.position, client.transform.position, workTime);
            }
        }

        yield return new WaitForSeconds(workTime);
        if (client == null || !client.CanContinueService || client.docHolder == null) { FinishAction(false); yield break; }
        
        // --- НАНЕСЕНИЕ УРОНА СТОЛУ ---
        if (durability != null)
        {
            durability.Degrade(Random.Range(2f, 5f));
        }
        // -----------------------------
        
        // --- 4. АНИМАЦИЯ: Выдаем сертификат ---
        if (flyingDoc != null) Destroy(flyingDoc); // Уничтожаем старый бланк на столе

        // Ищем префаб сертификата в DocumentHolder'е клиента
        GameObject certificatePrefab = client.docHolder.GetPrefabForType(DocumentType.Certificate2); 
        
        if (certificatePrefab != null && deskPoint != null && clientHand != null && client.stateMachine != null)
        {
            GameObject newCertGO = Instantiate(certificatePrefab, deskPoint.position, deskPoint.rotation);
            outgoingDocument = newCertGO;
            DocumentMover mover = newCertGO.GetComponent<DocumentMover>() ?? newCertGO.AddComponent<DocumentMover>();
            bool arrived = false;
            mover.StartMove(clientHand, () => {
                if (client != null && client.docHolder != null) {
                     // Клиент "получает" прилетевший документ
                     client.docHolder.ReceiveTransferredDocument(DocumentType.Certificate2, newCertGO); 
                } else {
                     Destroy(newCertGO); // Клиент ушел, пока документ летел
                }
                arrived = true;
            });
            yield return new WaitUntil(() => arrived || newCertGO == null || client == null);
            if (client == null || !client.CanContinueService) { FinishAction(false); yield break; }
        }
        else 
        {
            // Если анимация не удалась, используем старый метод
            if (client.stateMachine != null) 
                client.docHolder.SetDocument(DocumentType.Certificate2); 
        }
        // --- Конец анимации выдачи ---

        // 5. Отправляем в кассу
        if (client.stateMachine != null) // Проверяем, что клиент еще тут
        {
            client.billToPay += 250; 
            clerk.thoughtBubble?.ShowPriorityMessage("Готово! Пройдите в кассу.", 3f, Color.green);
            client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
            client.stateMachine.SetState(ClientState.MovingToGoal);
        }
        
        documentCommitted = true;
        clerk.ServiceComplete();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}