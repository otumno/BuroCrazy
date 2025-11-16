using UnityEngine;
using System.Collections;
using System.Linq;

public class ServiceAtOfficeDeskExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var clerk = staff as ClerkController;
        if (clerk == null || clerk.assignedWorkstation == null)
        {
            FinishAction(false);
            yield break;
        }

        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedWorkstation.deskId);
        var client = zone?.GetOccupyingClients().FirstOrDefault();

        if (client == null)
        {
            FinishAction(false);
            yield break;
        }

        clerk.SetState(ClerkController.ClerkState.Working);
        
        if (client.docHolder.GetCurrentDocumentType() == DocumentType.None || 
            (client.docHolder.GetCurrentDocumentType() != DocumentType.Form1 && client.docHolder.GetCurrentDocumentType() != DocumentType.Form2))
        {
            clerk.thoughtBubble?.ShowPriorityMessage("Сначала возьмите\nправильный бланк!", 3f, Color.yellow);
            yield return new WaitForSeconds(2f);
            client.stateMachine.GoGetFormAndReturn();
            FinishAction(true);
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
                FinishAction(true); // Считаем успехом, т.к. ошибка найдена
                yield break;
            }
        }
        
        clerk.thoughtBubble?.ShowPriorityMessage("Обрабатываю...", 3f, Color.white);
        yield return new WaitForSeconds(Random.Range(2f, 4f));
        
        DocumentType requiredDocType = client.docHolder.GetCurrentDocumentType();
        DocumentType newDocType = DocumentType.None;
        int billAmount = 0;

        if(requiredDocType == DocumentType.Form1 && clerk.activeActions.Any(a => a.actionType == ActionType.ProcessDocumentCat1))
        {
            newDocType = DocumentType.Certificate1;
            billAmount = 100;
        }
        else if (requiredDocType == DocumentType.Form2 && clerk.activeActions.Any(a => a.actionType == ActionType.ProcessDocumentCat2))
        {
            newDocType = DocumentType.Certificate2;
            billAmount = 250;
        }

        if(newDocType == DocumentType.None)
        {
            clerk.thoughtBubble?.ShowPriorityMessage("Я не занимаюсь\nтакими документами.", 3f, Color.red);
            FinishAction(false);
            yield break;
        }
        
        client.docHolder.SetDocument(newDocType);
        client.billToPay += billAmount;

        clerk.thoughtBubble?.ShowPriorityMessage("Готово! Пройдите в кассу.", 3f, Color.green);
        client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
        client.stateMachine.SetState(ClientState.MovingToGoal);
        
        clerk.ServiceComplete();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}