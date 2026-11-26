// Файл: Assets/Scripts/Data/Actions/CoverYourTracksExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class CoverYourTracksExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.accounts.tracks"), 4f, Color.magenta);
        
        float workDuration = 15f * (1f - staff.skills.paperworkMastery * 0.5f);
        yield return new WaitForSeconds(workDuration);

        int corruptionRemoved = Random.Range(50, 151);
        FinancialLedgerManager.Instance.globalCorruptionScore = Mathf.Max(0, FinancialLedgerManager.Instance.globalCorruptionScore - corruptionRemoved);
        
        staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.accounts.clear"), 2f, Color.green);
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}