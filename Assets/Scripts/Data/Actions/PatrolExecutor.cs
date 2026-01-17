// Assets/Scripts/Data/Actions/PatrolExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class PatrolExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        // ИСПРАВЛЕНИЕ: GetComponent вместо is GuardMovement
        var guard = staff.GetComponent<GuardMovement>();
        if (guard == null) 
        {
            FinishAction(false);
            yield break;
        }

        // Запускаем корутину патруля, которая прописана внутри GuardMovement
        // Мы ждем, пока она не прервется извне (этот Action прерываемый)
        yield return staff.StartCoroutine(guard.PatrolRoutine());

        // Если PatrolRoutine завершилась сама (например, точек нет)
        FinishAction(true);
    }
}