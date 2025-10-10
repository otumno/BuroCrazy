// Файл: Assets/Scripts/Data/Actions/ProcessDocumentCat1Executor.cs

using UnityEngine;
using System.Collections;
using System.Linq;

public class ProcessDocumentCat1Executor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedServicePoint == null)
        {
            FinishAction();
            yield break;
        }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedServicePoint.deskId);
        if (zone == null) { FinishAction(); yield break; }

        ClientPathfinding client = zone.GetOccupyingClients().FirstOrDefault();
        if (client == null)
        {
            FinishAction();
            yield break;
        }

        // --- НОВАЯ ЛОГИКА ---
        // Проверяем, есть ли у клерка вообще действие "Проверить документ"
        bool canCheckDocuments = clerk.activeActions.Any(a => a.actionType == ActionType.CheckDocument);

        if (canCheckDocuments)
        {
            clerk.thoughtBubble?.ShowPriorityMessage("Проверяю...", 2f, Color.yellow);
            yield return new WaitForSeconds(Random.Range(1f, 3f));

            float documentErrorPercent = (1f - client.documentQuality) * 100f;
            bool errorFound = false;
            
            if (documentErrorPercent > 10f)
            {
                float chanceToSpotErrors = clerk.skills != null ? clerk.skills.pedantry : 0.5f;
                if (Random.value < chanceToSpotErrors)
                {
                    errorFound = true;
                }
            }

            if (errorFound)
            {
                Debug.Log($"<color=orange>Клерк {clerk.name} нашел ошибку в документе клиента {client.name}.</color>");
                clerk.thoughtBubble?.ShowPriorityMessage("Здесь ошибка!\nНужно переделать.", 3f, Color.red);
                yield return new WaitForSeconds(2f);
                
                client.stateMachine.GoGetFormAndReturn();
                
                // Действие завершено (отрицательный результат)
                FinishAction();
                yield break;
            }
        }
        
        // --- Этап обработки (если проверка не нужна или пройдена) ---
        clerk.thoughtBubble?.ShowPriorityMessage("Обрабатываю...", 3f, Color.white);
        
        DocumentType docTypeInHand = client.docHolder.GetCurrentDocumentType();
        GameObject prefabToFly = client.docHolder.GetPrefabForType(docTypeInHand);
        client.docHolder.SetDocument(DocumentType.None);

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
        
        yield return new WaitForSeconds(Random.Range(2f, 4f));
        
        DocumentType newDocType = DocumentType.Certificate1;
        client.billToPay += 100;

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
        
        clerk.thoughtBubble?.ShowPriorityMessage("Готово!\nПройдите в кассу.", 3f, Color.green);
        client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
        client.stateMachine.SetState(ClientState.MovingToGoal);
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}