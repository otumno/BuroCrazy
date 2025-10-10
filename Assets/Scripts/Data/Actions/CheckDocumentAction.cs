using UnityEngine;
using System.Linq;

// Атрибут, который добавит опцию создания этого ассета в меню Unity
[CreateAssetMenu(fileName = "Action_CheckDocument", menuName = "Bureau/Actions/CheckDocument")]
public class CheckDocumentAction : StaffAction
{
    // Этот метод определяет, может ли сотрудник выполнить это действие ПРЯМО СЕЙЧАС.
    // Теперь это действие не выполняется само по себе, поэтому всегда возвращаем false.
    public override bool AreConditionsMet(StaffController staff)
    {
        return false; 
    }

    // Этот метод говорит системе, какой скрипт-ИСПОЛНИТЕЛЬ нужно запустить.
    // Поскольку исполнителя больше нет, возвращаем null.
    public override System.Type GetExecutorType()
    {
        return null;
    }
}