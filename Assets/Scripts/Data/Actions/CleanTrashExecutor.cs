using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Utilities;

public class CleanTrashExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Уборку на полпути не бросаем

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker)) { FinishAction(false); yield break; }

        MessPoint messPoint = MessManager.Instance.GetSortedMessList(worker.transform.position)
            .FirstOrDefault(m => m != null && m.type == MessPoint.MessType.Trash);
        
        // ВАЖНО: Если мусора нет, ОБЯЗАТЕЛЬНО FinishAction(false) перед выходом!
        if (messPoint == null) { FinishAction(false); yield break; }

        // Идем к мусору с таймаутом
        worker.SetState(ServiceWorkerController.WorkerState.GoingToMess);
        
        float moveTimeout = 15f;
        staff.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, messPoint.transform.position, staff.gameObject));
        
        while (staff.AgentMover.IsMoving() && moveTimeout > 0)
        {
            moveTimeout -= Time.deltaTime;
            if (messPoint == null) break; // Кто-то другой убрал мусор, пока мы шли
            yield return null;
        }

        staff.AgentMover.Stop();

        // Проверяем, не исчез ли мусор и дошли ли мы
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
        
        bool canFindValuables = staff.activeActions.Any(a => a.actionType == ActionType.FindValuablesInTrash);
        float chance = 0.1f + (worker.skills.paperworkMastery * 0.4f);

        if (canFindValuables && Random.value < chance)
        {
            staff.thoughtBubble?.ShowPriorityMessage("Ого, да это же...", 2f, Color.green);
            yield return new WaitForSeconds(2f);

            int moneyFound = Random.Range(10, 51);
            PlayerWallet.Instance?.AddMoney(moneyFound, "Находка в мусоре", IncomeType.Shadow);
            
            var cashierDesk = ScenePointsRegistry.Instance.GetServicePointByID(-1);
            if (cashierDesk != null)
            {
                // Идем к кассе с таймаутом
                moveTimeout = 15f;
                staff.AgentMover.SetPath(PathfindingUtility.BuildPathTo(staff.transform.position, cashierDesk.clerkStandPoint.position, staff.gameObject));
                
                while (staff.AgentMover.IsMoving() && moveTimeout > 0)
                {
                    moveTimeout -= Time.deltaTime;
                    yield return null;
                }
                
                staff.thoughtBubble?.ShowPriorityMessage("Положу здесь.", 2f, Color.gray);
                yield return new WaitForSeconds(1.5f);
            }
        }
        else
        {
            staff.thoughtBubble?.ShowPriorityMessage("*Вжик-вжик*", 2f, Color.cyan);
            yield return new WaitForSeconds(worker.cleaningTimeTrash);
        }

        if (messPoint != null) Destroy(messPoint.gameObject);

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        Managers.AchievementManager.Instance?.UnlockAchievement("Achv_CleanerStory");
        FinishAction(true);
    }
}