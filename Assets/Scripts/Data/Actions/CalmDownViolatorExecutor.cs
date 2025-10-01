using UnityEngine;
using System.Collections;

public class CalmDownViolatorExecutor : ActionExecutor
{
	public override bool IsInterruptible => false;
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is GuardMovement guard))
        {
            FinishAction();
            yield break;
        }

        // 1. Получаем цель из GuardManager
        ClientPathfinding violator = GuardManager.Instance.GetViolatorToHandle();
        if (violator == null)
        {
            FinishAction();
            yield break;
        }

        // Сообщаем менеджеру, что мы взяли эту задачу, чтобы другой охранник не побежал
        GuardManager.Instance.MarkTaskAsTaken(violator);
        guard.SetState(GuardMovement.GuardState.Chasing);
        Debug.Log($"<color=red>{guard.name} начинает преследование нарушителя {violator.name}!</color>");

        // 2. Включаем ускорение и преследуем цель
        guard.AgentMover.ApplySpeedMultiplier(guard.chaseSpeedMultiplier);

        while (violator != null && Vector2.Distance(guard.transform.position, violator.transform.position) > 2f)
        {
            // Используем прямое преследование без вейпоинтов
            guard.AgentMover.StartDirectChase(violator.transform.position);
            yield return null; // Ждем один кадр и повторяем
        }

        guard.AgentMover.StopDirectChase();
        guard.AgentMover.ApplySpeedMultiplier(1f); // Возвращаем нормальную скорость

        if (violator == null)
        {
            // Если нарушитель исчез, пока мы бежали
            FinishAction();
            yield break;
        }

        // 3. Разговор
        guard.SetState(GuardMovement.GuardState.Talking);
        Debug.Log($"{guard.name} разговаривает с {violator.name}.");
        yield return new WaitForSeconds(guard.talkTime);

        // 4. Результат
        if (Random.value > 0.5f)
        {
            violator.CalmDownAndLeave();
            Debug.Log($"{violator.name} успокоен и отправлен на выход.");
        }
        else
        {
            violator.CalmDownAndReturnToQueue();
            Debug.Log($"{violator.name} успокоен и возвращен в очередь.");
        }
        
        // Начисляем очко протокола за успешное действие
        guard.unwrittenReportPoints++;

        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction();
    }
}