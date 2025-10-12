using UnityEngine;
using System.Collections;
using System.Linq;

public class ProcessDocumentCat1Executor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedWorkstation == null)
        {
            FinishAction();
            yield break;
        }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        if (zone == null) { FinishAction(); yield break; }

        ClientPathfinding client = zone.GetOccupyingClients().FirstOrDefault();
        if (client == null || !client.documentChecked)
        {
            FinishAction();
            yield break;
        }

        clerk.thoughtBubble?.ShowPriorityMessage("Processing...", 3f, Color.white);
        
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
                mover.StartMove(clerk.assignedWorkstation.documentPointOnDesk, () => 
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
            GameObject newDocOnDesk = Instantiate(newDocPrefab, clerk.assignedWorkstation.documentPointOnDesk.position, Quaternion.identity);
            if (client.stampSound != null) { AudioSource.PlayClipAtPoint(client.stampSound, clerk.assignedWorkstation.documentPointOnDesk.position); } 
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

        client.documentChecked = false;
        clerk.thoughtBubble?.ShowPriorityMessage("Done! Please go to the cashier.", 3f, Color.green);
        client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
        client.stateMachine.SetState(ClientState.MovingToGoal);
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}