using UnityEngine;
using System.Collections;
using System.Linq;

public class HandleSituationExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Помощь клиенту - важное дело!

    protected override IEnumerator ActionRoutine()
{
    // --- НАЧАЛО НОВОЙ ЛОГИКИ ---

    // 1. Проверяем, может ли сотрудник помочь удаленно
    if (staff is ClerkController clerk && clerk.assignedWorkstation != null)
    {
        // Проверяем, находится ли клерк на своем рабочем месте
        float distanceToPost = Vector2.Distance(clerk.transform.position, clerk.assignedWorkstation.clerkStandPoint.position);
        if (distanceToPost < 0.5f) // Находится у стола
        {
            ClientPathfinding confusedClient = ClientPathfinding.FindClosestConfusedClient(staff.transform.position);
            if (confusedClient != null)
            {
                // Проверяем, находится ли клиент в "зоне досягаемости"
                float distanceToClient = Vector2.Distance(clerk.transform.position, confusedClient.transform.position);
                if (distanceToClient < 7f) // Условная "дальность крика" в 7 юнитов
                {
                    // ПОМОГАЕМ УДАЛЕННО!
                    staff.thoughtBubble?.ShowPriorityMessage("Молодой человек, вам куда?", 3f, Color.cyan);
                    yield return new WaitForSeconds(2f);

                    Waypoint correctGoal = DetermineCorrectGoalForClient(confusedClient);
                    confusedClient.stateMachine.GetHelpFromIntern(correctGoal); // Используем тот же механизм помощи

                    Debug.Log($"{staff.name} помог клиенту {confusedClient.name} УДАЛЕННО.");
                    ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
                    FinishAction();
                    yield break; // Завершаем действие
                }
            }
        }
    }

    // --- КОНЕЦ НОВОЙ ЛОГИКИ ---

    // 2. Если помочь удаленно не удалось - выполняем старую логику (идем к клиенту)
    ClientPathfinding clientToHelp = ClientPathfinding.FindClosestConfusedClient(staff.transform.position);
    if (clientToHelp == null) { FinishAction(); yield break; }

    staff.thoughtBubble?.ShowPriorityMessage("Вижу, нужна помощь...", 2f, Color.cyan);

    staff.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, clientToHelp.transform.position, staff.gameObject));
    yield return new WaitUntil(() => !staff.AgentMover.IsMoving());

    if (clientToHelp == null) { FinishAction(); yield break; }

    staff.thoughtBubble?.ShowPriorityMessage("Вам куда?", 3f, Color.white);
    yield return new WaitForSeconds(2f);

    Waypoint goal = DetermineCorrectGoalForClient(clientToHelp);
    clientToHelp.stateMachine.GetHelpFromIntern(goal);

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