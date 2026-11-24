using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Utilities;

public class ArchiveDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var archivist = staff;
        if (archivist == null) { FinishAction(false); yield break; }

        var source = ArchiveManager.Instance != null ? ArchiveManager.Instance.GetStackToProcess() : null;
        if (source == null)
        {
            FinishAction(false);
            yield break;
        }

        var transfer = new DocumentStackTransfer(source, 1);
        RegisterCleanup(transfer.Dispose);
        if (transfer.Count == 0) { FinishAction(false); yield break; }

        var stackHolder = archivist.GetComponent<StackHolder>();
        RegisterCleanup(() => { if (stackHolder != null) stackHolder.HideStack(); });
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

        float filingTime = Random.Range(2f, 4f);
        ShowProcessing(targetCabinet.transform.position, filingTime);
        yield return new WaitForSeconds(filingTime);
        transfer.Archive();
        stackHolder?.HideStack();

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}
