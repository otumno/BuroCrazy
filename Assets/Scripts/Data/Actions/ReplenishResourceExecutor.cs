// Assets/Scripts/Data/Actions/ReplenishResourceExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Gameplay;
using Utilities;

public class ReplenishResourceExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var manager = staff as OfficeManagerController;
        if (manager == null) { FinishAction(false); yield break; }

        // 1. Ищем самый пустой контейнер
        var targetContainer = Object.FindObjectsByType<ResourceContainer>(FindObjectsSortMode.None)
            .Where(c => c.NeedsReplenishment)
            .OrderBy(c => c.currentAmount) // Сначала самые пустые
            .FirstOrDefault();

        if (targetContainer == null) { FinishAction(false); yield break; }

        // 2. Ищем источник ресурса (склад)
        var source = Object.FindObjectsByType<ResourceSource>(FindObjectsSortMode.None)
            .FirstOrDefault(s => s.resourceType == targetContainer.resourceType);

        if (source == null)
        {
            manager.thoughtBubble?.ShowPriorityMessage("Где склад?!", 2f, Color.red);
            FinishAction(false);
            yield break;
        }

        // 3. Идем на склад
        manager.SetState(OfficeManagerController.ManagerState.RefillingWater); // Используем пока это состояние для всего
        manager.thoughtBubble?.ShowPriorityMessage($"Нужна {GetResourceName(targetContainer.resourceType)}...", 2f, Color.white);
        
        yield return staff.StartCoroutine(manager.MoveToTarget(source.transform.position, OfficeManagerController.ManagerState.Idle));

        // Анимация "берет со склада"
        yield return new WaitForSeconds(1f);
        
        // Визуализация в руках (упрощенно)
        var stackHolder = manager.GetComponent<StackHolder>();
        if (targetContainer.resourceType == ResourceType.Paper) stackHolder?.ShowStack(5, 5); // Показываем пачку бумаги
        // Для воды можно добавить отдельный объект, но пока пусть так

        manager.thoughtBubble?.ShowPriorityMessage("Несу!", 2f, Color.cyan);

        // 4. Идем к контейнеру
        yield return staff.StartCoroutine(manager.MoveToTarget(targetContainer.transform.position, OfficeManagerController.ManagerState.CarryingDoc));

        // 5. Пополняем
        yield return new WaitForSeconds(1.5f); // Время установки бутыли/бумаги
        targetContainer.Replenish();
        
        stackHolder?.HideStack(); // Прячем руки

        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        manager.SetState(OfficeManagerController.ManagerState.Idle);
        FinishAction(true);
    }

    private string GetResourceName(ResourceType type)
    {
        return type == ResourceType.Water ? "Вода" : "Бумага";
    }
}