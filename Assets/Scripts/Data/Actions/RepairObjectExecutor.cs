// Assets/Scripts/Data/Actions/RepairObjectExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;
using Utilities;

public class RepairObjectExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Нельзя прерывать починку

    protected override IEnumerator ActionRoutine()
    {
        var worker = staff as ServiceWorkerController;
        if (worker == null || DurabilityManager.Instance == null) 
        { 
            FinishAction(false); 
            yield break; 
        }

        // ОПТИМИЗАЦИЯ: Получаем самую приоритетную цель от менеджера
        var target = DurabilityManager.Instance.GetPriorityRepairTarget(worker.transform.position);

        if (target == null) 
        {
            FinishAction(false); 
            yield break;
        }

        // 1. Идем к объекту
        worker.SetState(ServiceWorkerController.WorkerState.GoingToMess); // Или GoingToRepair, если добавите в enum
        worker.thoughtBubble?.ShowPriorityMessage("Иду чинить!", 2f, Color.cyan);
        
        // Ищем точку взаимодействия (если это рабочий стол - идем к месту клерка, иначе просто к объекту)
        Vector3 targetPos = target.transform.position;
        var sp = target.GetComponent<ServicePoint>();
        if (sp != null && sp.clerkStandPoint != null) targetPos = sp.clerkStandPoint.position;

        yield return staff.StartCoroutine(worker.MoveToTarget(targetPos, ServiceWorkerController.WorkerState.Cleaning));

        // 2. Проверяем по прибытии (вдруг починили или уничтожили)
        if (target == null || target.currentHealth >= target.maxHealth)
        {
            FinishAction(true); // Уже не актуально
            yield break;
        }

        // 3. Чиним
        worker.SetState(ServiceWorkerController.WorkerState.Cleaning);
        worker.thoughtBubble?.ShowPriorityMessage("*Ремонт*", 3f, Color.gray);
        
        // Визуал инструмента
        if (worker.broomTransform != null) worker.broomTransform.gameObject.SetActive(true);

        // Время починки: 5 секунд * процент повреждения
        float damagePercent = 1f - (target.currentHealth / target.maxHealth);
        float repairDuration = 5f * damagePercent;
        
        yield return new WaitForSeconds(repairDuration);

        if (target != null) target.Repair(); // Восстанавливаем полностью

        if (worker.broomTransform != null) worker.broomTransform.gameObject.SetActive(false);

        // Награда и опыт
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        
        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        FinishAction(true);
    }
}