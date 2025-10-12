using UnityEngine;
using System.Collections;
using System.Linq;

public class ServiceAtOfficeDeskExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var clerk = staff as ClerkController;
        // Используем 'assignedWorkstation'
        if (clerk == null || clerk.assignedWorkstation == null)
        {
            FinishAction();
            yield break;
        }

        // Используем 'assignedWorkstation'
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        var client = zone?.GetOccupyingClients().FirstOrDefault();

        if (client == null)
        {
            FinishAction();
            yield break;
        }

        clerk.SetState(ClerkController.ClerkState.Working);
        
        if (client.docHolder.GetCurrentDocumentType() == DocumentType.None)
        {
            clerk.thoughtBubble?.ShowPriorityMessage("Сначала возьмите\nбланк со стола!", 3f, Color.yellow);
            yield return new WaitForSeconds(2f);
            client.stateMachine.GoGetFormAndReturn();
            FinishAction();
            yield break;
        }
        
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
                if (Random.value < chanceToSpotErrors) errorFound = true;
            }

            if (errorFound)
            {
                clerk.thoughtBubble?.ShowPriorityMessage("Здесь ошибка!\nНужно переделать.", 3f, Color.red);
                yield return new WaitForSeconds(2f);
                client.stateMachine.GoGetFormAndReturn();
                FinishAction();
                yield break;
            }
        }
        
        clerk.thoughtBubble?.ShowPriorityMessage("Обрабатываю...", 3f, Color.white);
        
        // Используем 'assignedWorkstation'
        int deskId = clerk.assignedWorkstation.deskId;
        DocumentType docTypeInHand = client.docHolder.GetCurrentDocumentType();
        client.docHolder.SetDocument(DocumentType.None);
        
        yield return new WaitForSeconds(Random.Range(2f, 4f));
        
        DocumentType newDocType = (deskId == 1) ? DocumentType.Certificate1 : DocumentType.Certificate2;
        int billAmount = (deskId == 1) ? 100 : 250;
        client.billToPay += billAmount;

        if (client.stampSound != null) AudioSource.PlayClipAtPoint(client.stampSound, staff.transform.position);
        yield return new WaitForSeconds(1.5f);
        client.docHolder.SetDocument(newDocType);

        clerk.thoughtBubble?.ShowPriorityMessage("Готово! Пройдите в кассу.", 3f, Color.green);
        client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
        client.stateMachine.SetState(ClientState.MovingToGoal);
        
        clerk.ServiceComplete();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}