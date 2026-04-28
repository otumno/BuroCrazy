using UnityEngine;
using DialogueSystem.Data;

[System.Serializable]
public class PhoneContact
{
    public string id;
    public string displayName;
    public bool isUnlocked;
    public StaffController.Role associatedRole; // для вызова сотрудников (старое)

    // --- НОВЫЕ ПОЛЯ ---
    public ServiceType serviceType;
    public int cost;                         // стоимость вызова
    public int maxDailyUses = 1;             // лимит в день
    public DialogueGraph mainDialogue;       // диалог при возможности звонка
    public DialogueGraph limitDialogue;      // диалог при исчерпании лимита

    [HideInInspector] public int remainingUsesToday; // runtime

    public bool CanCallToday() => remainingUsesToday > 0;
}

public enum ServiceType
{
    CallStaff,      // старый тип (вызов сотрудника)
    Repair,         // ремонт всей мебели
    Cleaning,       // вызов бригады уборщиков (будущее)
    Loan,           // займ
    Discount,       // скидка на услуги
    RemoveStrike,   // убрать страйк
    Heal,           // восстановить HP
    Clown           // клоун для снижения стресса
}