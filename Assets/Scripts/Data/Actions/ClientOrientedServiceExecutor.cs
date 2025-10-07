using UnityEngine;
using System.Collections;
using System.Linq;

public class ClientOrientedServiceExecutor : ActionExecutor
{
    public override bool IsInterruptible => true; // Разговор можно прервать, если появится что-то важнее

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedServicePoint == null)
        {
            FinishAction();
            yield break;
        }

        // Находим клиента
        var zone = ClientSpawner.GetZoneByDeskId(clerk.assignedServicePoint.deskId);
        if (zone == null) { FinishAction(); yield break; }

        ClientPathfinding client = zone.GetOccupyingClients().FirstOrDefault();
        if (client == null)
        {
            FinishAction();
            yield break;
        }

        // --- Логика действия ---
        
        // 1. Показываем мысль и ждем
        clerk.thoughtBubble?.ShowPriorityMessage("Как ваш день? Минуточку...", 3f, Color.green);
        yield return new WaitForSeconds(3f); // Тратим 3 секунды на разговор

        // 2. ГЛАВНЫЙ ЭФФЕКТ: Увеличиваем максимальное время терпения клиента
        float patienceBonus = 30f; // Даем клиенту дополнительные 30 секунд терпения
        client.totalPatienceTime += patienceBonus;

        Debug.Log($"{staff.name} применил клиентоориентированность к {client.name}. Терпение увеличено на {patienceBonus} сек.");
        
        // 3. Начисляем опыт и завершаем
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
}