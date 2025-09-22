// Файл: SkillDescriptor.cs
using UnityEngine;

// Enum для безопасной передачи типа навыка
public enum SkillType
{
    PaperworkMastery,
    SedentaryResilience,
    Pedantry,
    SoftSkills,
    Corruption
}

public static class SkillDescriptor
{
    // Главный метод, который вызывает нужный "переводчик"
    public static string GetDescriptionForSkill(SkillType type, float value)
    {
        switch (type)
        {
            case SkillType.PaperworkMastery:
                return GetPaperworkMasteryDescription(value);
            case SkillType.SedentaryResilience:
                return GetSedentaryResilienceDescription(value);
            case SkillType.Pedantry:
                return GetPedantryDescription(value);
            case SkillType.SoftSkills:
                return GetSoftSkillsDescription(value);
            case SkillType.Corruption:
                return GetCorruptionDescription(value);
            default:
                return "Неизвестный навык";
        }
    }

    private static string GetPaperworkMasteryDescription(float value)
    {
        if (value >= 1.0f) return "Бумажная Машина";
        if (value >= 0.75f) return "Почти Бумажная Машина";
        if (value >= 0.5f) return "Гражданин";
        if (value >= 0.25f) return "Почти Свой Человек";
        return "Свой Человек";
    }

    private static string GetSedentaryResilienceDescription(float value)
    {
        if (value >= 1.0f) return "Железный Зад";
        if (value >= 0.75f) return "Вросший в Кресло";
        if (value >= 0.5f) return "Умеренный Ходун";
        if (value >= 0.25f) return "Почти Шило";
        return "Шило в Одном Месте";
    }

    private static string GetPedantryDescription(float value)
    {
        if (value >= 1.0f) return "Душнила";
        if (value >= 0.75f) return "Зануда";
        if (value >= 0.5f) return "Коллега";
        if (value >= 0.25f) return "Почти на Чилле";
        return "На Чилле";
    }
    
    private static string GetSoftSkillsDescription(float value)
    {
        if (value >= 1.0f) return "Душа Компании";
        if (value >= 0.75f) return "Экстраверт";
        if (value >= 0.5f) return "Норм";
        if (value >= 0.25f) return "Почти Интроверт";
        return "Интроверт";
    }

    private static string GetCorruptionDescription(float value)
    {
        // Эти описания могут быть более завуалированными
        if (value >= 1.0f) return "Весь мир - возможности";
        if (value >= 0.75f) return "Знает, как жить";
        if (value >= 0.5f) return "Жизнь такова...";
        if (value >= 0.25f) return "Не без греха";
        return "Чистый лист";
    }
}