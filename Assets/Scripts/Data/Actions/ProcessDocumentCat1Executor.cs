// Assets/Scripts/Data/Actions/ProcessDocumentCat1Executor.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Gameplay; // Подключаем пространство имен с OfficeObjectDurability
using Clinch;
using Enums;

public class ProcessDocumentCat1Executor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        yield return ReturnToWorkstation();
        if (!IsAtWorkstation) { FinishAction(false); yield break; }
        var clerk = staff as ClerkController;
        // Проверки на null
        if (clerk == null || clerk.assignedWorkstation == null) 
        { 
            FinishAction(false); 
            yield break; 
        }

        var zone = clerk.assignedWorkstation.GetComponentInParent<LimitedCapacityZone>();
        var client = zone?.GetOccupyingClients().FirstOrDefault(c => CanServeAtWorkstation(c));
        
        // Бронебойный поиск физически ближайшего клиента у стола
        if (client == null && clerk.assignedWorkstation != null)
        {
            client = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None)
                .FirstOrDefault(c => CanServeAtWorkstation(c));
        }

        if (!CanServeAtWorkstation(client) || client.docHolder == null)
        {
            FinishAction(false);
            yield break;
        }

        if (!TryBeginClientService(client)) { FinishAction(false); yield break; }
        var cashierDestination = ClientSpawner.GetCashierZone()?.waitingWaypoint;
        if (cashierDestination == null)
        {
            Debug.LogWarning("[ClientServiceTrace] No cashier destination; document and bill unchanged.", clerk);
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
                staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.call_director"), 2f, Color.yellow);
                yield return new WaitWhile(() => clinchToTrigger != null && clinchToTrigger.IsActive && CanServeAtWorkstation(client));
                yield return WaitForClientService(client, 2.5f); // Даём реакции проявиться
            }
        }
        // После WaitWhile ОБЯЗАТЕЛЬНАЯ ПРОВЕРКА для всех экзекуторов:
        if (!CanServeAtWorkstation(client))
        {
            // Клиент обиделся и ушел по таймауту клинча
            if (staff is ClerkController c) c.SetState(ClerkController.ClerkState.Working);
            FinishAction(false);
            yield break;
        }
        // --- КЛИНЧ-ПРОВЕРКА (КОНЕЦ) ---

        // --- ИНТЕГРАЦИЯ ИЗНОСА (НАЧАЛО) ---
        // 1. Получаем компонент прочности (если он есть)
        var durability = clerk.assignedWorkstation.GetComponent<OfficeObjectDurability>();
        
        // 2. Если компонент ЕСТЬ и стол СЛОМАН -> прерываемся
        if (durability != null && !durability.IsUsable())
        {
            clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.call_janitor"), 3f, Color.red);
            
            // Сбрасываем состояние клерка, чтобы он не завис
            clerk.SetState(ClerkController.ClerkState.Working); 
            FinishAction(false);
            yield break;
        }

        // 3. Считаем эффективность (если компонента нет, множитель будет 1.0)
        float efficiency = Mathf.Max(.05f, durability != null ? durability.GetEfficiencyMultiplier() : 1.0f);
        // --- ИНТЕГРАЦИЯ ИЗНОСА (КОНЕЦ) ---

        clerk.SetState(ClerkController.ClerkState.Working);
        
        // 1. Проверяем, правильный ли бланк у клиента
        if (client.docHolder.GetCurrentDocumentType() != DocumentType.Form1)
        {
            clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.wrong_form"), 3f, Color.yellow);
            client.ApplyStressJump(client.stressJump_Refusal);
            client.stateMachine.GoGetFormAndReturn();
            FinishAction(true); // Задача выполнена (клиент отправлен)
            yield break;
        }
        
        // 2. Проверяем документ на ошибки (если клерк умеет)
        bool canCheckDocuments = clerk.activeActions.Any(a => a != null && a.actionType == ActionType.CheckDocument);
        if (canCheckDocuments)
        {
            clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.checking"), 2f, Color.yellow);
            yield return WaitForClientService(client, Random.Range(1f, 3f));
            if (!CanServeAtWorkstation(client)) { FinishAction(false); yield break; }
            if (Random.value < (1f - client.documentQuality) && Random.value < clerk.skills.pedantry)
            {
                clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.form_error"), 3f, Color.red);
                client.ApplyStressJump(client.stressJump_Refusal);
                yield return WaitForClientService(client, 2f);
                if (!CanServeAtWorkstation(client)) { FinishAction(false); yield break; }
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
            float flightDeadline = Time.time + 30f;
            yield return new WaitUntil(() => arrived || currentClientDocObject == null || deskPoint == null ||
                !CanServeAtWorkstation(client) || Time.time >= flightDeadline);
            if (!arrived || !CanServeAtWorkstation(client) || deskPoint == null) { FinishAction(false); yield break; }
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

        clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.processing_cat1"), 3f, Color.white);
        
        // --- ПРИМЕНЕНИЕ ЭФФЕКТИВНОСТИ ---
        // Время работы увеличивается, если стол изношен (делим на efficiency)
        float workTime = Random.Range(2f, 4f) / efficiency;

        ShowProcessing(client.transform.position, workTime);

        yield return WaitForClientService(client, workTime);
        if (!CanServeAtWorkstation(client) || client.docHolder == null) { FinishAction(false); yield break; }
        
        // --- НАНЕСЕНИЕ УРОНА СТОЛУ ---
        if (durability != null)
        {
            durability.Degrade(Random.Range(2f, 5f)); // Наносим урон столу
        }
        // -----------------------------
        
        // --- 4. АНИМАЦИЯ: Выдаем сертификат ---
        if (flyingDoc != null) Destroy(flyingDoc); // Уничтожаем старый бланк на столе

        // Ищем префаб сертификата в DocumentHolder'е клиента
        GameObject certificatePrefab = client.docHolder.GetPrefabForType(DocumentType.Certificate1); 
        
        if (certificatePrefab != null && deskPoint != null && clientHand != null && client.stateMachine != null)
        {
            GameObject newCertGO = Instantiate(certificatePrefab, deskPoint.position, deskPoint.rotation);
            outgoingDocument = newCertGO;
            DocumentMover mover = newCertGO.GetComponent<DocumentMover>() ?? newCertGO.AddComponent<DocumentMover>();
            bool arrived = false;
            // Commit the paper only after the worker confirms the appointment is still valid.
            mover.StartMove(clientHand, () => arrived = true);
            float flightDeadline = Time.time + 30f;
            yield return new WaitUntil(() => arrived || newCertGO == null || clientHand == null ||
                !CanServeAtWorkstation(client) || Time.time >= flightDeadline);
            if (!arrived || newCertGO == null || !CanServeAtWorkstation(client) || client.docHolder == null)
            { FinishAction(false); yield break; }
            client.docHolder.ReceiveTransferredDocument(DocumentType.Certificate1, newCertGO);
        }
        else 
        {
            // Если анимация не удалась, используем старый метод
            if (client.stateMachine != null) 
                client.docHolder.SetDocument(DocumentType.Certificate1);
        }
        // --- Конец анимации выдачи ---

        // 5. Отправляем в кассу
        if (client.stateMachine != null) // Проверяем, что клиент еще тут
        {
            client.billToPay += 100;
            clerk.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.go_to_cashier"), 3f, Color.green);
            client.stateMachine.SetGoal(cashierDestination);
            client.stateMachine.SetState(ClientState.MovingToGoal);
        }
        
        documentCommitted = true;
        clerk.ServiceComplete();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}