// Файл: Assets/Scripts/Characters/Controllers/Actions/ProcessDocumentExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;

public class ProcessDocumentExecutor : ActionExecutor
{
    public override bool IsInterruptible => false; // Нельзя прерывать обслуживание клиента на полпути

    protected override IEnumerator ActionRoutine()
    {
        if (!(staff is ClerkController clerk) || clerk.assignedServicePoint == null)
        {
            FinishAction();
            yield break;
        }

        // 1. Находим зону и клиента, которого нужно обслужить
        LimitedCapacityZone myZone = ClientSpawner.GetZoneByDeskId(clerk.assignedServicePoint.deskId);
        ClientPathfinding clientToServe = myZone?.GetOccupyingClients().FirstOrDefault();

        if (clientToServe == null)
        {
            // Клиент ушел, пока мы собирались его обслужить
            FinishAction();
            yield break;
        }

        clerk.SetState(ClerkController.ClerkState.Working);
        clerk.thoughtBubble?.ShowPriorityMessage("Следующий!", 2f, Color.white);
        
        // 2. Передаем клиента на обслуживание
        // ВАЖНО: В ClientStateMachine уже есть вся сложная логика обслуживания (проверка документов, выдача новых и т.д.)
        // Нам нужно просто "передать" клиента клерку.
        clerk.AssignClient(clientToServe);
        
        // 3. Ждем, пока клиент не закончит обслуживание
        // Мы будем ждать, пока клиент не покинет зону или его состояние не изменится с "внутри зоны".
        yield return new WaitUntil(() => clientToServe == null || clientToServe.stateMachine.GetTargetZone() != myZone);

        Debug.Log($"Клерк {clerk.name} завершил обслуживание клиента {clientToServe?.name ?? "ушедшего клиента"}.");
        
        // 4. Сообщаем о завершении и добавляем документ в стопку на столе
        clerk.ServiceComplete();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);

        // 5. Возвращаемся в состояние ожидания и завершаем действие
        clerk.SetState(ClerkController.ClerkState.Working); // Остаемся в рабочем состоянии
        FinishAction();
    }
}