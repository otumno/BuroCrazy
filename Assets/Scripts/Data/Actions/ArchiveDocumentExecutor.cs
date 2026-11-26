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

        var sourceDesk = source.GetComponentInParent<ServicePoint>();
        var pickup = sourceDesk != null && sourceDesk.internCollectionPoint != null ? sourceDesk.internCollectionPoint : source.transform;
        yield return archivist.MoveToTarget(pickup.position, "Working");
        if (source == null || pickup == null || !ArchiveManager.IsWithinReach(archivist.transform, pickup.position))
        {
            Debug.LogWarning("[ArchiveTrace] Pickup unreachable; papers left at source.", archivist);
            FinishAction(false);
            yield break;
        }
        var transfer = new DocumentStackTransfer(source, 1);
        RegisterCleanup(transfer.Dispose);
        if (transfer.Count == 0) { FinishAction(false); yield break; }

        var stackHolder = archivist.GetComponent<StackHolder>();
        RegisterCleanup(() => { if (stackHolder != null) stackHolder.HideStack(); });
        stackHolder?.ShowSingleDocumentSprite();

        // System skills belong to the rank. Only configurable tactics are copied into activeActions at hire.
        bool canCorrectDocuments = archivist.activeActions.Any(a => a is CorrectDocumentAction) ||
            (archivist.currentRank != null && archivist.currentRank.unlockedActions.Exists(a => a is CorrectDocumentAction));
        if (canCorrectDocuments && DocumentQualityManager.Instance != null && DocumentQualityManager.Instance.GetCurrentAverageErrorRate() > 0)
        {
            float correctionChance = 0.1f + (archivist.skills.pedantry * 0.4f);
            if (Random.value < correctionChance)
            {
                archivist.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.archive.correcting"), 3f, Color.yellow);
                yield return new WaitForSeconds(Random.Range(3f, 5f));

                float correctionStrength = 0.2f + (archivist.skills.paperworkMastery * 0.5f);
                DocumentQualityManager.Instance?.CorrectWorstDocument(correctionStrength);
            }
        }

        var targetCabinet = ArchiveManager.Instance != null ? ArchiveManager.Instance.GetRandomCabinet() : null;
        if (targetCabinet == null) { FinishAction(false); yield break; }

        archivist.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.archive.filing"), 3f, Color.gray);
        yield return archivist.MoveToTarget(targetCabinet.transform.position, "Working");
        if (targetCabinet == null || !ArchiveManager.IsWithinReach(archivist.transform, targetCabinet.transform.position))
        {
            Debug.LogWarning("[ArchiveTrace] Cabinet unreachable; returning paper to source.", archivist);
            FinishAction(false);
            yield break;
        }

        float filingTime = Random.Range(2f, 4f);
        ShowProcessing(targetCabinet.transform.position, filingTime);
        yield return new WaitForSeconds(filingTime);
        if (targetCabinet == null) { FinishAction(false); yield break; }
        transfer.Archive();
        stackHolder?.HideStack();

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}
