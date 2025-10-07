using UnityEngine;
using System.Collections;
using System.Linq;

public class ProcessDocumentCat2Executor : ActionExecutor
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
        if (client == null || !client.documentChecked)
        {
            FinishAction();
            yield break;
        }

        clerk.thoughtBubble?.ShowPriorityMessage("Обрабатываю (Кат. 2)...", 3f, Color.white);
        
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
                mover.StartMove(clerk.assignedServicePoint.documentPointOnDesk, () => { transferToClerkComplete = true; if (flyingDoc != null) Destroy(flyingDoc); });
                yield return new WaitUntil(() => transferToClerkComplete);
            } 
        }

        yield return new WaitForSeconds(Random.Range(3f, 5f)); // Документы 2-й категории обрабатываются дольше
        
        // --- ГЛАВНОЕ ОТЛИЧИЕ: Выдаем Сертификат 2 ---
        DocumentType newDocType = DocumentType.Certificate2;
        client.billToPay += 250; // И услуга стоит дороже

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
                mover.StartMove(client.docHolder.handPoint, () => { client.docHolder.ReceiveTransferredDocument(newDocType, newDocOnDesk); transferToClientComplete = true; });
                yield return new WaitUntil(() => transferToClientComplete);
            } 
        }

        client.documentChecked = false;
        
        clerk.thoughtBubble?.ShowPriorityMessage("Готово!\nПройдите в кассу.", 3f, Color.green);
        client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
        client.stateMachine.SetState(ClientState.MovingToGoal);
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}