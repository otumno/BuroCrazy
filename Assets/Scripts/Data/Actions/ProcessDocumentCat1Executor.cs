// Файл: Assets/Scripts/Data/Actions/ProcessDocumentCat1Executor.cs
using UnityEngine;
using System.Collections;
using System.Linq;

public class ProcessDocumentCat1Executor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var clerk = staff as ClerkController;
        if (clerk == null || clerk.assignedWorkstation == null) { FinishAction(false); yield break; }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        var client = zone?.GetOccupyingClients().FirstOrDefault();

        if (client == null || client.docHolder == null) { FinishAction(false); yield break; }

        clerk.SetState(ClerkController.ClerkState.Working);
        
        // 1. Проверяем, правильный ли бланк у клиента
        if (client.docHolder.GetCurrentDocumentType() != DocumentType.Form1)
        {
            clerk.thoughtBubble?.ShowPriorityMessage("Это не тот бланк,\nвозьмите другой.", 3f, Color.yellow);
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
        yield return new WaitForSeconds(Random.Range(2f, 4f)); // Время на "печать"
        
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