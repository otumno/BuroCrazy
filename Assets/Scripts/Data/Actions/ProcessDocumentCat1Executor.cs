// Assets/Scripts/Data/Actions/ProcessDocumentCat1Executor.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Gameplay; // Подключаем пространство имен с OfficeObjectDurability

public class ProcessDocumentCat1Executor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var clerk = staff as ClerkController;
        // Проверки на null
        if (clerk == null || clerk.assignedWorkstation == null) 
        { 
            FinishAction(false); 
            yield break; 
        }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        var client = zone?.GetOccupyingClients().FirstOrDefault();

        if (client == null || client.docHolder == null) 
        { 
            FinishAction(false); 
            yield break; 
        }

        // --- ИНТЕГРАЦИЯ ИЗНОСА (НАЧАЛО) ---
        // 1. Получаем компонент прочности (если он есть)
        var durability = clerk.assignedWorkstation.GetComponent<OfficeObjectDurability>();
        
        // 2. Если компонент ЕСТЬ и стол СЛОМАН -> прерываемся
        if (durability != null && !durability.IsUsable())
        {
            clerk.thoughtBubble?.ShowPriorityMessage("Стол сломан!\nЗовите уборщика!", 3f, Color.red);
            
            // Сбрасываем состояние клерка, чтобы он не завис
            clerk.SetState(ClerkController.ClerkState.Working); 
            FinishAction(false);
            yield break;
        }

        // 3. Считаем эффективность (если компонента нет, множитель будет 1.0)
        float efficiency = (durability != null) ? durability.GetEfficiencyMultiplier() : 1.0f;
        // --- ИНТЕГРАЦИЯ ИЗНОСА (КОНЕЦ) ---

        clerk.SetState(ClerkController.ClerkState.Working);
        
        // 1. Проверяем, правильный ли бланк у клиента
        if (client.docHolder.GetCurrentDocumentType() != DocumentType.Form1)
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
            if (Random.value < (1f - client.documentQuality) && Random.value < clerk.skills.pedantry)
            {
                clerk.thoughtBubble?.ShowPriorityMessage("Здесь ошибка!\nНужно переделать.", 3f, Color.red);
                client.ApplyStressJump(client.stressJump_Refusal);
                yield return new WaitForSeconds(2f);
                client.stateMachine.GoGetFormAndReturn();
                FinishAction(true); // Задача выполнена (ошибка найдена)
                yield break;
            }
        }
        
        // --- 3. АНИМАЦИЯ: Забираем документ у клиента ---
        DocumentHolder clientDocHolder = client.docHolder;
        Transform clientHand = clientDocHolder?.handPoint;
        Transform deskPoint = clerk.assignedWorkstation.documentPointOnDesk;
        GameObject currentClientDocObject = (clientHand != null && clientHand.childCount > 0) ? clientHand.GetChild(0).gameObject : null;
        GameObject flyingDoc = null;

        if (currentClientDocObject != null && deskPoint != null)
        {
            clientDocHolder.SetDocument(DocumentType.None); // Убираем документ из данных
            DocumentMover mover = currentClientDocObject.AddComponent<DocumentMover>();
            bool arrived = false;
            mover.StartMove(deskPoint, () => { arrived = true; });
            yield return new WaitUntil(() => arrived);
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

        clerk.thoughtBubble?.ShowPriorityMessage("Обрабатываю (Кат. 1)...", 3f, Color.white);
        
        // --- ПРИМЕНЕНИЕ ЭФФЕКТИВНОСТИ ---
        // Время работы увеличивается, если стол изношен (делим на efficiency)
        float workTime = Random.Range(2f, 4f) / efficiency;

        // Пытаемся достать префаб иконки обработки через ссылки клерка
        var refs = clerk.GetComponent<StaffPrefabReferences>();
        if (refs != null && refs.processingIconPrefab != null)
        {
            GameObject iconObj = Instantiate(refs.processingIconPrefab);
            ProcessingAnimationUI animScript = iconObj.GetComponent<ProcessingAnimationUI>();
            if (animScript != null)
            {
                animScript.Play(clerk.transform.position, client.transform.position, workTime);
            }
        }

        yield return new WaitForSeconds(workTime);
        
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
            DocumentMover mover = newCertGO.AddComponent<DocumentMover>();
            bool arrived = false;
            mover.StartMove(clientHand, () => {
                if (client != null && client.docHolder != null) {
                     // Клиент "получает" прилетевший документ
                     client.docHolder.ReceiveTransferredDocument(DocumentType.Certificate1, newCertGO);
                } else {
                     Destroy(newCertGO); // Клиент ушел, пока документ летел
                }
                arrived = true;
            });
            yield return new WaitUntil(() => arrived);
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
            clerk.thoughtBubble?.ShowPriorityMessage("Готово! Пройдите в кассу.", 3f, Color.green);
            client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
            client.stateMachine.SetState(ClientState.MovingToGoal);
        }
        
        clerk.ServiceComplete();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}