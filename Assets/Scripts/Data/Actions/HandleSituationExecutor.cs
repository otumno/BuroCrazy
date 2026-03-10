using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Utilities;

public class HandleSituationExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        if (staff is ClerkController clerk && clerk.assignedWorkstation != null)
        {
            float distanceToPost = Vector2.Distance(clerk.transform.position, clerk.assignedWorkstation.clerkStandPoint.position);
            if (distanceToPost < 0.5f)
            {
                ClientPathfinding confusedClient = ClientPathfinding.FindClosestConfusedClient(staff.transform.position);
                if (confusedClient != null)
                {
                    float distanceToClient = Vector2.Distance(clerk.transform.position, confusedClient.transform.position);
                    HandleSituationAction handleAction = actionData as HandleSituationAction;
                    if (handleAction != null && distanceToClient < handleAction.remoteHelpRadius)
                    {
                        staff.thoughtBubble?.ShowPriorityMessage("Молодой человек, вам куда?", 3f, Color.cyan);
                        yield return new WaitForSeconds(2f);

                        Waypoint correctGoal = DetermineCorrectGoalForClient(confusedClient);
                        confusedClient.stateMachine.GetHelpFromIntern(correctGoal);
                        
                        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
                        FinishAction(true);
                        yield break;
                    }
                }
            }
        }

        ClientPathfinding clientToHelp = ClientPathfinding.FindClosestConfusedClient(staff.transform.position);
        if (clientToHelp == null) { FinishAction(false); yield break; }

        // --- АНТИ-КЛОН (Race Condition Fix) ---
        // Проверяем еще раз: вдруг в этот же самый кадр клиента уже занял другой стажер!
        if (clientToHelp.assignedHelper != null && clientToHelp.assignedHelper != staff) 
        { 
            FinishAction(false); 
            yield break; 
        }
        // Ставим жесткую бронь!
        clientToHelp.assignedHelper = staff; 
        // -------------------------------------

        // Устанавливаем визуальный статус
        if (staff is InternController intern) intern.SetState(InternController.InternState.HelpingConfused);

        staff.thoughtBubble?.ShowPriorityMessage("Вижу, нужна помощь...", 2f, Color.cyan);

        staff.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, clientToHelp.transform.position, staff.gameObject));
        
        // --- АНТИ-ЗАСТРЕВАНИЕ (Timeout Fix) ---
        float moveTimeout = 15f; // Максимум 15 секунд на попытку дойти
        while (staff.AgentMover.IsMoving() && moveTimeout > 0)
        {
            moveTimeout -= Time.deltaTime;
            
            // Если клиент ушел или пропал, пока мы шли
            if (clientToHelp == null || clientToHelp.stateMachine.GetCurrentState() != ClientState.Confused)
            {
                staff.AgentMover.Stop();
                FinishAction(false);
                yield break;
            }
            yield return null;
        }

        if (moveTimeout <= 0)
        {
            // Мы застряли (бодались с кем-то 15 секунд)!
            staff.AgentMover.Stop();
            staff.thoughtBubble?.ShowPriorityMessage("Не могу пройти!", 2f, Color.red);
            clientToHelp.assignedHelper = null; // Освобождаем клиента для других
            FinishAction(false);
            yield break;
        }
        // --------------------------------------

        staff.thoughtBubble?.ShowPriorityMessage("Вам куда?", 2f, Color.white);
        yield return new WaitForSeconds(1.5f); // Слушаем клиента

        Waypoint goal = DetermineCorrectGoalForClient(clientToHelp);
        
        // --- ПЕРЕВОДИМ ЦЕЛЬ НА ЧЕЛОВЕЧЕСКИЙ ЯЗЫК ---
        string destinationName = "нужное окно";
        if (clientToHelp.billToPay > 0 || clientToHelp.mainGoal == ClientGoal.PayTax) destinationName = "Кассу";
        else if (clientToHelp.mainGoal == ClientGoal.GetCertificate1) destinationName = "Окно №1";
        else if (clientToHelp.mainGoal == ClientGoal.GetCertificate2) destinationName = "Окно №2";
        else if (clientToHelp.mainGoal == ClientGoal.VisitToilet) destinationName = "Туалет";

        staff.thoughtBubble?.ShowPriorityMessage($"Вам в {destinationName}!", 2.5f, Color.green);
        yield return new WaitForSeconds(1.5f); // Даем время прочитать и осознать
        // ------------------------------------------

        clientToHelp.stateMachine.GetHelpFromIntern(goal);
        Managers.ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        
        if (staff is InternController i) i.SetState(InternController.InternState.Patrolling);
        
        FinishAction(true); // Успешно закончили!
    }

    private Waypoint DetermineCorrectGoalForClient(ClientPathfinding client)
    {
        if (client.billToPay > 0) return ClientSpawner.GetCashierZone()?.waitingWaypoint;
        switch (client.mainGoal)
        {
            case ClientGoal.PayTax: return ClientSpawner.GetCashierZone()?.waitingWaypoint;
            case ClientGoal.GetCertificate1: return ClientSpawner.GetDesk1Zone()?.waitingWaypoint;
            case ClientGoal.GetCertificate2: return ClientSpawner.GetDesk2Zone()?.waitingWaypoint;
            case ClientGoal.VisitToilet: return ClientSpawner.GetToiletZone()?.waitingWaypoint;
            default: return ClientQueueManager.Instance.ChooseNewGoal(client);
        }
    }
}
