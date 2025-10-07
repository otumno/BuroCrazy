using UnityEngine;
using System.Collections;
using System.Linq;

public class ProcessDocumentCat1Executor : ActionExecutor
{
    // Это действие также не стоит прерывать на полпути
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedServicePoint == null)
        {
            FinishAction();
            yield break;
        }

        // --- Шаг 1: Находим нашего клиента (та же логика, что и в CheckDocumentExecutor) ---
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedServicePoint.deskId);
        if (zone == null) { FinishAction(); yield break; }

        ClientPathfinding client = zone.GetOccupyingClients().FirstOrDefault();
        if (client == null || !client.documentChecked) // Доп. проверка, что документ точно проверен
        {
            FinishAction();
            yield break;
        }

        // --- Шаг 2: Логика обработки документа ---
        clerk.thoughtBubble?.ShowPriorityMessage("Обрабатываю...", 3f, Color.white);
        
        // Забираем у клиента старый бланк
        DocumentType docTypeInHand = client.docHolder.GetCurrentDocumentType();
        GameObject prefabToFly = client.docHolder.GetPrefabForType(docTypeInHand); 
        client.docHolder.SetDocument(DocumentType.None); // Убираем документ из рук клиента
        
        // Анимация передачи документа на стол
        bool transferToClerkComplete = false;
        if (prefabToFly != null) 
        { 
            GameObject flyingDoc = Instantiate(prefabToFly, client.docHolder.handPoint.position, Quaternion.identity);
            DocumentMover mover = flyingDoc.GetComponent<DocumentMover>(); 
            if (mover != null) 
            {
                mover.StartMove(clerk.assignedServicePoint.documentPointOnDesk, () => 
                { 
                    transferToClerkComplete = true; 
                    if (flyingDoc != null) Destroy(flyingDoc); 
                });
                yield return new WaitUntil(() => transferToClerkComplete);
            } 
        }

        // Имитируем работу с документом
        yield return new WaitForSeconds(Random.Range(2f, 4f));
        
        // Определяем, какой новый документ выдать
        DocumentType newDocType = (clerk.assignedServicePoint.deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2;
        client.billToPay += 100; // Выставляем счет за услугу

        // Анимация выдачи нового документа клиенту
        GameObject newDocPrefab = client.docHolder.GetPrefabForType(newDocType);
        bool transferToClientComplete = false; 
        if (newDocPrefab != null) 
        { 
            GameObject newDocOnDesk = Instantiate(newDocPrefab, clerk.assignedServicePoint.documentPointOnDesk.position, Quaternion.identity);
            if (client.stampSound != null) { AudioSource.PlayClipAtPoint(client.stampSound, clerk.assignedServicePoint.documentPointOnDesk.position); } 
            yield return new WaitForSeconds(1.5f);
            
            DocumentMover mover = newDocOnDesk.GetComponent<DocumentMover>(); 
            if (mover != null) 
            {
                mover.StartMove(client.docHolder.handPoint, () => 
                { 
                    client.docHolder.ReceiveTransferredDocument(newDocType, newDocOnDesk); 
                    transferToClientComplete = true; 
                });
                yield return new WaitUntil(() => transferToClientComplete);
            } 
        }

        // --- Шаг 3: Завершение обслуживания ---
        
        // Сбрасываем флаг, чтобы при следующем обращении документ снова пришлось проверять
        client.documentChecked = false;
        
        // Отправляем клиента в кассу оплачивать счет
        clerk.thoughtBubble?.ShowPriorityMessage("Готово!\nПройдите в кассу.", 3f, Color.green);
        client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
        client.stateMachine.SetState(ClientState.MovingToGoal);
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);

        FinishAction();
    }
}