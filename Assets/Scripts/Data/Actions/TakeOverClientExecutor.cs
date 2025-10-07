using UnityEngine;
using System.Collections;
using System.Linq;

public class TakeOverClientExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var registrar = staff as ClerkController;
        if (registrar == null) { FinishAction(); yield break; }

        // --- Шаг 1: Найти ближайшего застрявшего клиента ---
        var stuckClient = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None)
            .Where(c => c.stateMachine.GetCurrentState() == ClientState.AtRegistration && 
                        c.stateMachine.MyServiceProvider != null && 
                        !c.stateMachine.MyServiceProvider.IsAvailableToServe)
            .OrderBy(c => Vector3.Distance(staff.transform.position, c.transform.position))
            .FirstOrDefault();

        if (stuckClient == null)
        {
            FinishAction(); // Пока мы думали, проблема решилась сама
            yield break;
        }
        
        // --- Шаг 2: Перехватить клиента ---
        registrar.thoughtBubble?.ShowPriorityMessage("Не стойте здесь, я вами займусь!", 3f, Color.blue);
        yield return new WaitForSeconds(1f);

        // Получаем новую точку назначения (у стойки НАШЕГО регистратора)
        var newDestination = registrar.assignedServicePoint.clientStandPoint;
        
        // "Перевызываем" клиента к себе
        stuckClient.stateMachine.GetCalledToSpecificDesk(newDestination, stuckClient.stateMachine.MyQueueNumber, registrar);

        Debug.Log($"{registrar.name} перехватил клиента {stuckClient.name} от другого окна.");

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}