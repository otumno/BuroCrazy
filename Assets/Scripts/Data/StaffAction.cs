// Файл: Assets/Scripts/Data/StaffAction.cs

using UnityEngine;
using System.Collections.Generic;

// --- ДОБАВЛЕНО: Недостающий класс для описания влияния навыков ---
[System.Serializable]
public class SkillInfluence
{
    public SkillType skill; // Какой навык влияет
    public bool isPositiveEffect = true; // true = Увеличивает шанс, false = Уменьшает
    [Range(0f, 1f)]
    public float strength = 0.5f; // Сила влияния (0 = нет, 1 = максимум)
}
// -----------------------------------------------------------------

// Это базовый класс для всех "Карточек Дел". Он не будет создаваться напрямую.
public abstract class StaffAction : ScriptableObject
{
    [Header("Основная информация")]
    public ActionType actionType;
    public string displayName;
    [TextArea(2, 4)]
    public string description;

    [Header("Настройки геймплея")]
	[Tooltip("Приоритет этого действия. Чем выше число, тем важнее действие.")]
	public int priority = 0; // <-- ДОБАВЬТЕ ЭТУ СТРОКУ
    [Tooltip("Базовый шанс успеха (от 0.0 до 1.0)")]
    [Range(0f, 1f)]
    public float baseSuccessChance = 0.8f;
	

    [Tooltip("Минимальный итоговый шанс успеха (от 0.0 до 1.0)")]
    [Range(0f, 1f)]
    public float minSuccessChance = 0.3f;

    [Tooltip("Максимальный итоговый шанс успеха (от 0.0 до 1.0)")]
    [Range(0f, 1f)]
    public float maxSuccessChance = 0.95f;

    [Tooltip("Основная характеристика, влияющая на успех")]
    public SkillInfluence primarySkill;

    [Tooltip("Включить влияние второй характеристики?")]
    public bool useSecondarySkill;
    [Tooltip("Вторая (опциональная) характеристика")]
    public SkillInfluence secondarySkill;

    [Tooltip("Для циклических действий (патруль): как долго (в сек.) сотрудник будет их выполнять.")]
    public float actionDuration = 30f;

    [Tooltip("Для патруля: сколько точек нужно обойти за один цикл. Если 0, используется actionDuration.")]
    public int patrolPointsToVisit = 3;

    [Tooltip("На сколько секунд это действие уходит 'на перезарядку' после выполнения.")]
    public float actionCooldown = 60f;

    [Header("Требования и тип")]
    public int minRankRequired = 0;
    public List<StaffController.Role> applicableRoles;
    public bool isUnique = false;
	
	
    
    // НОВЫЙ МЕТОД: Каждое действие теперь само знает, можно ли его выполнить
    public abstract bool AreConditionsMet(StaffController staff);
    
    // НОВЫЙ МЕТОД: Каждое действие знает, какой скрипт-исполнитель ему нужен
    public abstract System.Type GetExecutorType();
}