using UnityEngine;
using System.Collections;

public class CorrectDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    protected override IEnumerator ActionRoutine()
    {
        var archivist = staff;
        if (archivist == null) { FinishAction(false); yield break; }

        archivist.thoughtBubble?.ShowPriorityMessage("Ищу ошибки...", 2f, Color.yellow);
        yield return new WaitForSeconds(Random.Range(4f, 7f));

        float correctionStrength = 0.2f + (archivist.skills.paperworkMastery * 0.5f);

        if (DocumentQualityManager.Instance.CorrectWorstDocument(correctionStrength))
        {
            ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        }

        FinishAction(true);
    }
}