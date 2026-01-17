// Assets/Scripts/Data/Actions/RepairObjectExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Gameplay;
using Utilities;

public class RepairObjectExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Нельзя прерывать починку

    protected override IEnumerator ActionRoutine()
    {
        var worker = staff as ServiceWorkerController;
        if (worker == null) { FinishAction(false); yield break; }

        // Ищем самый сломанный объект (наименьший % здоровья)
        var target = FindObjectsByType<OfficeObjectDurability>(FindObjectsSortMode.None)
            .Where(d => d.currentHealth < d.maxHealth)
            .OrderBy(d => d.currentHealth / d.maxHealth) // Сначала самые убитые
            .ThenBy(d => Vector3.Distance(worker.transform.position, d.transform.position)) // Потом ближайшие
            .FirstOrDefault();

        if (target == null) 
        {
            FinishAction(false); 
            yield break;
        }

        // 1. Идем к объекту
        worker.SetState(ServiceWorkerController.WorkerState.GoingToMess); // Используем существующее состояние "Иду к грязи" или добавьте GoingToRepair
        worker.thoughtBubble?.ShowPriorityMessage("Иду чинить!", 2f, Color.cyan);
        
        // Ищем точку рядом с объектом
        Vector3 targetPos = target.transform.position;
        // Если это стол (ServicePoint), идем к точке клерка
        var sp = target.GetComponent<ServicePoint>();
        if (sp != null && sp.clerkStandPoint != null) targetPos = sp.clerkStandPoint.position;

        yield return staff.StartCoroutine(worker.MoveToTarget(targetPos, ServiceWorkerController.WorkerState.Cleaning));

        // 2. Проверяем, не починили ли уже
        if (target.currentHealth >= target.maxHealth)
        {
            FinishAction(true); // Кто-то уже починил
            yield break;
        }

        // 3. Чиним
        worker.SetState(ServiceWorkerController.WorkerState.Cleaning); // Анимация уборки подойдет
        worker.thoughtBubble?.ShowPriorityMessage("*Стук молотком*", 3f, Color.gray);
        
        // Включаем визуал (швабру или молоток, если есть)
        if (worker.broomTransform != null) worker.broomTransform.gameObject.SetActive(true);

        // Время починки зависит от степени поломки (например, 5 сек на полную починку)
        float damagePercent = 1f - (target.currentHealth / target.maxHealth);
        float repairDuration = 5f * damagePercent;
        
        yield return new WaitForSeconds(repairDuration);

        target.Repair(); // Восстанавливаем полностью

        if (worker.broomTransform != null) worker.broomTransform.gameObject.SetActive(false);

        // Награда
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        
        worker.SetState(ServiceWorkerController.WorkerState.Idle);
        FinishAction(true);
    }
}