// Файл: DirectorOrder.cs
using UnityEngine;

// --- НОВЫЙ ENUM ДЛЯ ДЛИТЕЛЬНОСТИ ---
public enum OrderDuration
{
    SingleDay,
    Permanent
}

[CreateAssetMenu(fileName = "DirectorOrder", menuName = "My Game/Director Order")]
public class DirectorOrder : ScriptableObject
{
    [Header("Основная информация")]
    public string orderName;
    [TextArea(3, 5)]
    public string description;
    public Sprite icon;
	
	 [Header("Настройки геймплея")]
    [Tooltip("Допустимый процент ошибок для этого приказа (например, 0.1 для 10%)")]
    [Range(0f, 1f)]
    public float allowedDirectorErrorRate;

    [Header("Мета-настройки Приказа")]
    [Tooltip("Как долго действует приказ: один день или навсегда.")]
    public OrderDuration duration = OrderDuration.SingleDay;
    
    [Tooltip("Если true, этот приказ после выбора больше никогда не появится.")]
    public bool isOneTimeOnly = false;

    [Tooltip("Вес при выборе. Чем выше, тем чаще приказ будет появляться. Стандартный = 1.")]
    public float selectionWeight = 1f;

    [Header("Мгновенные эффекты (срабатывают 1 раз при выборе)")]
    [Tooltip("Разовая выплата денег при выборе приказа.")]
    public int oneTimeMoneyBonus = 0;

    [Tooltip("Убирает один страйк директора.")]
    public bool removeStrike = false;
    
    [Header("Постоянные или дневные эффекты")]
    [Tooltip("Сумма, которая будет добавляться каждый день в начале дня. Работает только с 'Permanent' длительностью.")]
    public int permanentDailyIncome = 0;

    [Tooltip("Множитель скорости движения всех сотрудников.")]
    public float staffMoveSpeedMultiplier = 1f;

    [Tooltip("Множитель скорости спавна клиентов. 0.5 - в два раза реже.")]
    public float clientSpawnRateMultiplier = 1f;

    [Tooltip("Множитель стресса, получаемого клерками.")]
    public float clerkStressGainMultiplier = 1f;

    // --- ПРИМЕРЫ ДОПОЛНИТЕЛЬНЫХ ЭФФЕКТОВ ---
    [Header("Дополнительные игровые модификаторы")]
    [Tooltip("Множитель терпения клиентов. >1 = терпят дольше, <1 = уходят быстрее.")]
    public float clientPatienceMultiplier = 1f;
    
    [Tooltip("Множитель шанса, что клиент создаст беспорядок. <1 = мусорят реже.")]
    public float messGenerationMultiplier = 1f;
    
    [Tooltip("Множитель скорости снятия стресса у персонала во время отдыха. >1 = отдыхают эффективнее.")]
    public float staffStressReliefMultiplier = 1f;

    [Tooltip("Принудительно отключает логику охраны.")]
    public bool disableGuards = false;
}