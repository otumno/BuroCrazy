using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Utilities;

public class SystematizeArchiveExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    protected override IEnumerator ActionRoutine()
    {
        var archivist = staff;
        var cabinets = ArchiveManager.Instance?.cabinets?.Where(c => c != null).ToArray();
        if (archivist == null || archivist.AgentMover == null || cabinets == null || cabinets.Length == 0) { FinishAction(false); yield break; }

        archivist.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.archive.organizing"), 5f, Color.gray);
        
        int pointsToVisit = 2;
        for (int i = 0; i < pointsToVisit; i++)
        {
            var randomCabinet = cabinets[Random.Range(0, cabinets.Length)];
            archivist.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, randomCabinet.transform.position, staff.gameObject));
            yield return new WaitUntil(() => !archivist.AgentMover.IsMoving());
            if (randomCabinet == null || !ArchiveManager.IsWithinReach(archivist.transform, randomCabinet.transform.position))
            { FinishAction(false); yield break; }
            yield return new WaitForSeconds(Random.Range(3f, 6f));
        }

        FinishAction(true);
    }
}