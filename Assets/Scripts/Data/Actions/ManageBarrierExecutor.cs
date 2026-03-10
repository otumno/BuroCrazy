// Assets/Scripts/Data/Actions/ManageBarrierExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class ManageBarrierExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var guard = staff.GetComponent<GuardMovement>();
        if (guard == null) { FinishAction(false); yield break; }

        // Находим дверь через GuardManager
        var barrier = GuardManager.Instance?.securityBarrier;
        if (barrier == null) { FinishAction(false); yield break; }

        // Идем к двери (точка взаимодействия должна быть рядом с дверью)
        yield return staff.StartCoroutine(staff.MoveToTarget(barrier.interactionPoint.position, "Idle"));

        // Проверяем, может кто-то уже открыл ее, пока мы шли?
        if (!barrier.IsActive())
        {
            staff.thoughtBubble?.ShowPriorityMessage("Открыто!", 2f, Color.green);
            
            // Заставляем дверь открыться в обход клика игрока
            barrier.ToggleBarrier();
            
            // Записываем открытие двери в отчет
            guard.unwrittenReportPoints++;
            
            // --- НОВЫЙ ЗВУК ИЗ SoundID ---
            if (AudioManager.Instance != null)
            {
                // Замените SoundID.UI_Click_Default на ваш ID открытия двери (например, SoundID.Door_Open)
                AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Click_Default);
            }
        }
        else
        {
            staff.thoughtBubble?.ShowPriorityMessage("Уже открыто...", 2f, Color.gray);
        }

        yield return new WaitForSeconds(1f); // Небольшая пауза для солидности
        guard.SetState(GuardMovement.GuardState.Idle);
        FinishAction(true);
    }
}