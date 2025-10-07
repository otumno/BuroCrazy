using UnityEngine;
using System.Collections;
using System.Linq;

public class SystematizeArchiveExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var archivist = staff;
        var cabinets = ArchiveManager.Instance.cabinets;
        if (archivist == null || !cabinets.Any()) { FinishAction(); yield break; }

        archivist.thoughtBubble?.ShowPriorityMessage("Навожу порядок...", 5f, Color.gray);

        // Просто ходим между 2-3 случайными шкафами
        int pointsToVisit = 2; 
        for (int i = 0; i < pointsToVisit; i++)
        {
            var randomCabinet = cabinets[Random.Range(0, cabinets.Count)];
            archivist.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, randomCabinet.transform.position, staff.gameObject));
            yield return new WaitUntil(() => !archivist.AgentMover.IsMoving());
            yield return new WaitForSeconds(Random.Range(3f, 6f));
        }

        FinishAction();
    }
}