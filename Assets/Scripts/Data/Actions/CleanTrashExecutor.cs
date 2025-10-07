using UnityEngine;
using System.Collections;
using System.Linq;

public class CleanTrashExecutor : ActionExecutor
{
    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ServiceWorkerController worker)) { FinishAction(); yield break; }

        // 1. Находим ближайший мусор
        MessPoint messPoint = MessManager.Instance.GetSortedMessList(worker.transform.position)
            .FirstOrDefault(m => m != null && m.type == MessPoint.MessType.Trash);

        if (messPoint == null) { FinishAction(); yield break; }

        worker.SetState(ServiceWorkerController.WorkerState.GoingToMess);
        yield return staff.StartCoroutine(worker.MoveToTarget(messPoint.transform.position, ServiceWorkerController.WorkerState.Cleaning));

        if (messPoint == null) { FinishAction(); yield break; }
        worker.SetState(ServiceWorkerController.WorkerState.Cleaning);

        // --- НАЧАЛО НОВОЙ ЛОГИКИ ---

        // 2. Проверяем, умеет ли уборщик искать ценности
        bool canFindValuables = staff.activeActions.Any(a => a.actionType == ActionType.FindValuablesInTrash);
        float chance = 0.1f + (worker.skills.paperworkMastery * 0.4f); // 10% база + до 40% от навыка

        if (canFindValuables && Random.value < chance)
        {
            // УСПЕХ! Нашли ценную бумагу
            worker.thoughtBubble?.ShowPriorityMessage("Ого, да это же...", 2f, Color.green);
            yield return new WaitForSeconds(2f);

            int moneyFound = Random.Range(10, 51);
            PlayerWallet.Instance?.AddMoney(moneyFound, "Находка в мусоре", IncomeType.Shadow);
            Debug.Log($"{worker.name} нашел в мусоре {moneyFound}$!");

            // Несем "находку" в кассу
            var cashierDesk = ScenePointsRegistry.Instance.GetServicePointByID(-1);
            if (cashierDesk != null)
            {
                yield return staff.StartCoroutine(worker.MoveToTarget(cashierDesk.clerkStandPoint.position, ServiceWorkerController.WorkerState.Cleaning));
                worker.thoughtBubble?.ShowPriorityMessage("Положу здесь.", 2f, Color.gray);
                yield return new WaitForSeconds(1.5f);
            }
        }
        else
        {
            // ПРОВАЛ или нет навыка: Просто убираем мусор
            yield return new WaitForSeconds(worker.cleaningTimeTrash);
        }

        // --- КОНЕЦ НОВОЙ ЛОГИКИ ---

        Destroy(messPoint.gameObject);
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);

        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        FinishAction();
    }
}