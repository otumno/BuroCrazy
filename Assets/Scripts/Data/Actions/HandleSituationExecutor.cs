using UnityEngine;
using System.Collections;
using System.Linq;

public class HandleSituationExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Помощь клиенту - важное дело!

    protected override IEnumerator ActionRoutine()
    {
        // --- Шаг 1: Найти цель ---
        ClientPathfinding confusedClient = ClientPathfinding.FindClosestConfusedClient(staff.transform.position);

        // Если, пока мы собирались, клиент уже "нашелся" или исчез
        if (confusedClient == null)
        {
            FinishAction();
            yield break;
        }

        staff.thoughtBubble?.ShowPriorityMessage("Вижу, нужна помощь...", 2f, Color.cyan);
        
        // --- Шаг 2: Подойти к клиенту ---
        // Используем универсальный AgentMover из StaffController
        staff.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, confusedClient.transform.position, staff.gameObject));
        yield return new WaitUntil(() => !staff.AgentMover.IsMoving());

        // Еще раз проверяем, не исчез ли клиент
        if (confusedClient == null)
        {
            FinishAction();
            yield break;
        }
        
        staff.thoughtBubble?.ShowPriorityMessage("Вам куда?", 3f, Color.white);
        yield return new WaitForSeconds(2f); // Имитируем диалог

        // --- Шаг 3: Определить правильную цель и направить ---
        Waypoint correctGoal = DetermineCorrectGoalForClient(confusedClient);

        // Используем метод, который уже есть у клиента для помощи от стажера
        confusedClient.stateMachine.GetHelpFromIntern(correctGoal);
        
        Debug.Log($"{staff.name} помог клиенту {confusedClient.name} и направил его к {correctGoal.name}");
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }

    // Вспомогательный метод для определения, куда на самом деле нужно клиенту
    private Waypoint DetermineCorrectGoalForClient(ClientPathfinding client)
    {
        // Эта логика частично повторяет то, что есть в ClientStateMachine,
        // но теперь она централизована здесь для действия "помощи".
        if (client.billToPay > 0)
        {
            return ClientSpawner.GetCashierZone().waitingWaypoint;
        }

        switch (client.mainGoal)
        {
            case ClientGoal.PayTax:
                return ClientSpawner.GetCashierZone().waitingWaypoint;
            case ClientGoal.GetCertificate1:
                return ClientSpawner.GetDesk1Zone().waitingWaypoint;
            case ClientGoal.GetCertificate2:
                return ClientSpawner.GetDesk2Zone().waitingWaypoint;
            case ClientGoal.VisitToilet:
                return ClientSpawner.GetToiletZone().waitingWaypoint;
            case ClientGoal.AskAndLeave:
            default:
                // Если цель непонятна, отправляем в общую очередь
                return ClientQueueManager.Instance.ChooseNewGoal(client);
        }
    }
}