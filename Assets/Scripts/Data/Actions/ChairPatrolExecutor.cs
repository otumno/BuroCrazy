// Файл: Assets/Scripts/Data/Actions/ChairPatrolExecutor.cs

using UnityEngine;
using System.Collections;
using System.Linq; // Добавляем для использования Linq

public class ChairPatrolExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;
    private ClerkController registrar;

    protected override IEnumerator ActionRoutine()
    {
        registrar = staff as ClerkController;
        if (registrar == null) { FinishAction(); yield break; }

        registrar.SetState(ClerkController.ClerkState.ChairPatrol); // Устанавливаем правильное состояние
        registrar.redirectionBonus = 0.25f;
        registrar.thoughtBubble?.ShowPriorityMessage("Готов к работе...", 5f, Color.gray);

        // --- НОВАЯ ЛОГИКА ---
        // Цикл теперь не бесконечный, а постоянно проверяет условие
        while (true)
        {
            // Находим зону, к которой приписан регистратор
            var zone = ClientSpawner.GetZoneByDeskId(registrar.assignedServicePoint.deskId);
            
            // Если в зоне появился клиент, то наше действие "Патруль на стуле" больше не актуально
            if (zone != null && zone.GetOccupyingClients().Any())
            {
                Debug.Log($"[ChairPatrol] {registrar.name} увидел клиента и завершает патруль, чтобы начать обслуживание.");
                break; // Выходим из цикла
            }

            yield return new WaitForSeconds(1f); // Проверяем каждую секунду
        }

        // Завершаем действие, чтобы ИИ мог выбрать следующее (обслуживание клиента)
        FinishAction();
    }

    private void OnDestroy()
    {
        if (registrar != null)
        {
            registrar.redirectionBonus = 0f;
        }
    }
}