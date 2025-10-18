using UnityEngine;
using System.Collections;
using System.Linq;

public class ArchiveDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var archivist = staff;
        if (archivist == null) { FinishAction(false); yield break; }

        if (!ArchiveManager.Instance.mainDocumentStack.TakeOneDocument())
        {
            FinishAction(false);
            yield break;
        }

        var stackHolder = archivist.GetComponent<StackHolder>();
        stackHolder?.ShowSingleDocumentSprite();

        bool canCorrectDocuments = archivist.activeActions.Any(a => a.actionType == ActionType.CorrectDocument);
        if (canCorrectDocuments && DocumentQualityManager.Instance.GetCurrentAverageErrorRate() > 0)
        {
            float correctionChance = 0.1f + (archivist.skills.pedantry * 0.4f);
            if (Random.value < correctionChance)
            {
                archivist.thoughtBubble?.ShowPriorityMessage("Тут ошибка... исправлю.", 3f, Color.yellow);
                yield return new WaitForSeconds(Random.Range(3f, 5f));

                float correctionStrength = 0.2f + (archivist.skills.paperworkMastery * 0.5f);
                DocumentQualityManager.Instance.CorrectWorstDocument(correctionStrength);
            }
        }

        var targetCabinet = ArchiveManager.Instance.GetRandomCabinet();
        if (targetCabinet == null) { FinishAction(false); yield break; }

        archivist.thoughtBubble?.ShowPriorityMessage("Архивирую...", 3f, Color.gray);
        archivist.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, targetCabinet.transform.position, staff.gameObject));
        yield return new WaitUntil(() => !archivist.AgentMover.IsMoving());

        yield return new WaitForSeconds(Random.Range(2f, 4f));
        stackHolder?.HideStack();

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}