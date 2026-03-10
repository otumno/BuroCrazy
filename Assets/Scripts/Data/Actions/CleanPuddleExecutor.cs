using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Utilities;

public class CleanPuddleExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Уборку на полпути не бросаем

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker))
        {
            FinishAction(false);
            yield break;
        }

        MessPoint messPoint = MessManager.Instance.GetSortedMessList(worker.transform.position)
            .FirstOrDefault(m => m != null && m.type == MessPoint.MessType.Puddle);
        
        // ВАЖНО: Если лужи нет, ОБЯЗАТЕЛЬНО FinishAction(false) перед выходом!
        if (messPoint == null) { FinishAction(false); yield break; }

        // Идем к луже с таймаутом
        worker.SetState(ServiceWorkerController.WorkerState.GoingToMess);
        
        float moveTimeout = 15f;
        staff.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, messPoint.transform.position, staff.gameObject));
        
        while (staff.AgentMover.IsMoving() && moveTimeout > 0)
        {
            moveTimeout -= Time.deltaTime;
            if (messPoint == null) break; // Кто-то другой (или Директор) убрал лужу, пока мы шли
            yield return null;
        }

        staff.AgentMover.Stop();

        // Проверяем, не исчезла ли лужа и дошли ли мы
        if (messPoint == null)
        {
            FinishAction(true); // Кто-то убрал за нас, считаем успехом
            yield break;
        }

        if (Vector2.Distance(staff.transform.position, messPoint.transform.position) > 2.5f)
        {
            staff.thoughtBubble?.ShowPriorityMessage("Не достать...", 2f, Color.red);
            FinishAction(false); // Не смогли дойти
            yield break;
        }

        // --- ПРОЦЕСС УБОРКИ ---
        worker.SetState(ServiceWorkerController.WorkerState.Cleaning);
        staff.thoughtBubble?.ShowPriorityMessage("*Вжик-вжик*", 2f, Color.cyan);
        
        yield return new WaitForSeconds(worker.cleaningTimePuddle);

        if (messPoint != null) Destroy(messPoint.gameObject);
        
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        FinishAction(true);
    }
}