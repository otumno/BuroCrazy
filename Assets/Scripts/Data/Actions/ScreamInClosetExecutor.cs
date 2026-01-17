// Assets/Scripts/Data/Actions/ScreamInClosetExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class ScreamInClosetExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        // Ищем чулан (janitorHomePoint)
        var closet = ScenePointsRegistry.Instance?.janitorHomePoint;

        if (closet != null)
        {
            // ИСПРАВЛЕНИЕ: closet.transform.position
            yield return staff.StartCoroutine(staff.MoveToTarget(closet.transform.position, "Crying"));
            
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.Voice_Secretary, transform.position); // Крик

            staff.thoughtBubble?.ShowPriorityMessage("ААААА!!!", 2f, Color.red);
            
            // Сброс стресса
            staff.ChangeStress(-50);
            yield return new WaitForSeconds(2f);
        }

        FinishAction(true);
    }
}