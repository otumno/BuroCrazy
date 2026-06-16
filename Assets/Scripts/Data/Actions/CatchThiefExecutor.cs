// Assets/Scripts/Data/Actions/CatchThiefExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class CatchThiefExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Нельзя прерывать погоню

    protected override IEnumerator ActionRoutine()
    {
        // ИСПРАВЛЕНИЕ: Получаем компонент
        var guard = staff.GetComponent<GuardMovement>();
        if (guard == null) 
        {
            FinishAction(false);
            yield break;
        }

        var thief = GuardManager.Instance?.currentThief;
        if (thief == null)
        {
            FinishAction(true); // Вор исчез
            yield break;
        }

        // Устанавливаем состояние (новое состояние из Enum)
        guard.SetState(GuardMovement.GuardState.ChasingThief);
        
        staff.thoughtBubble?.ShowPriorityMessage("Стой, ворюга!", 2f, Color.red);

        // Ускоряем (используем AgentMover через мост или напрямую через staff)
        float originalSpeed = staff.agentMover.moveSpeed;
        staff.agentMover.moveSpeed *= guard.chaseSpeedMultiplier;

        // Логика преследования
        while (thief != null && Vector3.Distance(staff.transform.position, thief.transform.position) > 1.5f)
        {
            // Обновляем цель каждые 0.2 сек
            staff.agentMover.SetTarget(thief.transform.position);
            yield return new WaitForSeconds(0.2f);
        }

        // Поймали!
        staff.agentMover.moveSpeed = originalSpeed; // Возвращаем скорость
        
        if (thief != null)
        {
            // Логика поимки (например, удаление вора или вывод из здания)
            // GuardManager.Instance.ArrestThief(thief); // Если есть такой метод
            staff.thoughtBubble?.ShowPriorityMessage("Попался!", 2f, Color.green);

            // Очки за отчет
            guard.unwrittenReportPoints++;

            // Ачивка: охранник впервые поймал вора
            Managers.AchievementManager.Instance?.UnlockAchievement("Achv_GuardStory");
        }

        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}