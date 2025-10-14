using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_HandleSituation", menuName = "Bureau/Actions/HandleSituation")]
public class HandleSituationAction : StaffAction
{
    [Header("Настройки для Регистратора")]
    [Tooltip("Радиус, в котором регистратор будет 'кричать' подсказки, не вставая с места.")]
    public float remoteHelpRadius = 4f; // Уменьшим радиус до 4

    public override bool AreConditionsMet(StaffController staff)
    {
        // Общие проверки: сотрудник не должен быть на перерыве
        if (staff.IsOnBreak())
        {
            return false;
        }

        // Находим ближайшего запутавшегося клиента
        ClientPathfinding confusedClient = ClientPathfinding.FindClosestConfusedClient(staff.transform.position);
        if (confusedClient == null)
        {
            return false; // Если таких клиентов нет, действие невозможно
        }
		
		// Проверяем, не назначен ли этот клиент уже другому сотруднику
    if (confusedClient.assignedHelper != null && confusedClient.assignedHelper != staff)
    {
        return false; // Не можем помочь, если кто-то другой уже назначен
    }

    // Если мы первый, кто нашел этого клиента, назначаем себя
    if (confusedClient.assignedHelper == null)
    {
        confusedClient.assignedHelper = staff;
    }
		
        // ----- НОВАЯ ЛОГИКА: Проверка роли -----
        // Если это регистратор, применяем особое правило
        if (staff is ClerkController clerk && clerk.role == ClerkController.ClerkRole.Registrar)
        {
            // Условие для регистратора: "Запутавшийся клиент находится в пределах моего радиуса помощи"
            return Vector2.Distance(staff.transform.position, confusedClient.transform.position) < remoteHelpRadius;
        }

        // Для всех остальных (например, стажёра) действует старое правило:
        // "Если есть хоть один запутавшийся клиент, действие возможно"
        return true;
    }

    public override System.Type GetExecutorType()
    {
        return typeof(HandleSituationExecutor);
    }
}