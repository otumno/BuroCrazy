using UnityEngine;
using System.Linq;

// Атрибут, который добавит опцию создания этого ассета в меню Unity
[CreateAssetMenu(fileName = "Action_CheckDocument", menuName = "Bureau/Actions/CheckDocument")]
public class CheckDocumentAction : StaffAction
{
    // Этот метод определяет, может ли сотрудник выполнить это действие ПРЯМО СЕЙЧАС.
    public override bool AreConditionsMet(StaffController staff)
    {
        // 1. Убеждаемся, что это клерк и он на работе (не на перерыве).
        if (!(staff is ClerkController clerk) || clerk.IsOnBreak())
        {
            return false;
        }

        // 2. Проверяем, находится ли клерк в рабочем состоянии у стойки.
        //    Это простое условие, которое мы можем усложнить в будущем.
        if (clerk.GetCurrentState() != ClerkController.ClerkState.Working)
        {
            return false;
        }
        
        // 3. Главное условие: есть ли перед клерком клиент с непроверенным документом?
        //    Для этого нам понадобится найти клиента, которого обслуживает клерк.
        //    (Логику поиска клиента мы добавим в Executor, здесь пока просто вернем true,
        //    если клерк просто сидит на рабочем месте и готов к работе).
        
        return true; 
    }

    // Этот метод говорит системе, какой скрипт-ИСПОЛНИТЕЛЬ нужно запустить для этого действия.
    public override System.Type GetExecutorType()
    {
        // Мы создадим этот скрипт на следующем шаге.
        return typeof(CheckDocumentExecutor);
    }
}