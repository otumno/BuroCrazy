using System.Collections;
using UnityEngine;

public class OperateBarrierExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        // Убеждаемся, что работаем с охранником
        if (!(staff is GuardMovement guard))
        {
            Debug.LogError("OperateBarrierExecutor может быть использован только на охраннике!");
            FinishAction();
            yield break;
        }

        // Находим барьер и точку взаимодействия
        var barrier = GuardManager.Instance.securityBarrier;
        if (barrier == null || barrier.guardInteractionPoint == null)
        {
            Debug.LogError("Точка взаимодействия с барьером не найдена!");
            FinishAction();
            yield break;
        }
        
        guard.SetState(GuardMovement.GuardState.OperatingBarrier);

        // 1. Идем к точке взаимодействия
        yield return staff.StartCoroutine(guard.MoveToTarget(barrier.guardInteractionPoint.position, GuardMovement.GuardState.OperatingBarrier));
        
        // 2. Пауза для имитации действия
        yield return new WaitForSeconds(2.0f);

        // 3. Выполняем действие в зависимости от времени суток
        string currentPeriodName = ClientSpawner.CurrentPeriodName;
        if (currentPeriodName == "Утро" && barrier.IsActive())
        {
            barrier.DeactivateBarrier(); // Открываем утром
        }
        else if (currentPeriodName == "Ночь" && !barrier.IsActive())
        {
            barrier.ActivateBarrier(); // Закрываем ночью
        }

        guard.SetState(GuardMovement.GuardState.Idle); // Возвращаемся в состояние ожидания

        // 4. Завершаем действие
        FinishAction();
    }
}