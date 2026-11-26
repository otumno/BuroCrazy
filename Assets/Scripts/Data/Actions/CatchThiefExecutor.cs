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
        
        staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.guard.stop_thief"), 2f, Color.red);

        // Ускоряем (используем AgentMover через мост или напрямую через staff)
        float originalSpeed = staff.agentMover.moveSpeed;
        RegisterCleanup(() => { if (staff != null && staff.agentMover != null) staff.agentMover.moveSpeed = originalSpeed; });
        staff.agentMover.moveSpeed *= guard.chaseSpeedMultiplier;

        // Логика преследования
        float chaseDeadline = Time.time + 20f;
        while (Time.time < chaseDeadline && thief != null && Vector3.Distance(staff.transform.position, thief.transform.position) > 1.5f)
        {
            // Обновляем цель каждые 0.2 сек
            staff.agentMover.SetTarget(thief.transform.position);
            yield return new WaitForSeconds(0.2f);
        }

        // Поймали!
        staff.agentMover.moveSpeed = originalSpeed; // Возвращаем скорость
        
        if (thief != null && Vector3.Distance(staff.transform.position, thief.transform.position) > 1.5f)
        { staff.AgentMover?.Stop(); FinishAction(false); yield break; }

        if (thief != null)
        {
            // Логика поимки (например, удаление вора или вывод из здания)
            thief.GetComponent<ClientPathfinding>()?.ForceLeave(ClientPathfinding.LeaveReason.Theft);
            if (GuardManager.Instance != null && GuardManager.Instance.currentThief == thief) GuardManager.Instance.currentThief = null;
            staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.guard.caught"), 2f, Color.green);

            // Очки за отчет
            guard.unwrittenReportPoints++;

            // Ачивка: охранник впервые поймал вора
            Managers.AchievementManager.Instance?.UnlockAchievement("Achv_GuardStory");
        }

        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}