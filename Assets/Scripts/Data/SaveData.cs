// Assets/Scripts/Data/SaveData.cs
using System.Collections.Generic;
using UnityEngine;
using Managers;
using Enums;
using Characters;
using StorySystem;

[System.Serializable]
public class SaveData
{
    // --- Глобальные данные ---
    public int day;
    public int money;
    public int archiveDocumentCount;
    public bool firstDayTutorialCompleted = false;
    
    // --- Создание директора ---
    [Tooltip("Код создания директора (A1B2C1D3E3)")]
    public string directorCreationCode = "";
    
    // --- Сюжет и Флаги ---
    public List<string> storyFlagKeys = new List<string>();
    public List<int> storyFlagValues = new List<int>();

    // --- Приказы Директора ---
    public List<string> activePermanentOrderNames;
    public List<string> completedOneTimeOrderNames;

    // --- Списки объектов ---
    public List<StaffSaveData> allStaffData;
    public List<DocumentStackSaveData> allDocumentStackData;
    
    // [НОВОЕ] Данные о прочности мебели
    public List<DurabilitySaveData> allDurabilityData;

    // --- Контакты телефона ---
    public List<string> unlockedContactIDs;

    public HashSet<string> watchedDialogues;

    // [НОВОЕ] Прогресс сюжетных арок (текущее прохождение)
    public List<ArcSaveData> arcProgress;

    // --- Система концовок ---
    [Tooltip("Баллы черт личности Директора (Law/Empathy/Mask/Ambition) — сериализуется как параллельные списки (JsonUtility не поддерживает Dictionary).")]
    public List<string> traitScoreKeys = new List<string>();
    public List<int> traitScoreValues = new List<int>();

    [Tooltip("true после успешного финала (книга учёта). При отстранении остаётся false.")]
    public bool gameCompleted = false;

    public Dictionary<string, int> GetTraitScoresDictionary()
    {
        var dict = new Dictionary<string, int>();
        if (traitScoreKeys == null || traitScoreValues == null) return dict;
        int count = Mathf.Min(traitScoreKeys.Count, traitScoreValues.Count);
        for (int i = 0; i < count; i++) dict[traitScoreKeys[i]] = traitScoreValues[i];
        return dict;
    }

    public void SetTraitScoresFromDictionary(Dictionary<string, int> dict)
    {
        traitScoreKeys = new List<string>();
        traitScoreValues = new List<int>();
        if (dict == null) return;
        foreach (var kv in dict)
        {
            traitScoreKeys.Add(kv.Key);
            traitScoreValues.Add(kv.Value);
        }
    }
}

[System.Serializable]
public struct StaffSaveData
{
    public string gameObjectName;
    public StaffController.StaffNameData nameData;
    public float stressLevel;
    public Vector3 position;
    
    // Характеристики
    public StaffController.Role role;
    public Gender gender;
    public int salary;
    public int experience;
    
    // Навыки
    public float paperworkMastery;
    public float sedentaryResilience;
    public float pedantry;
    public float softSkills;
    public float corruption;
    
    // Рабочее место и расписание
    public int assignedWorkstationId;
    public int scheduleTrackIndex;

    // Статистика посещаемости
    public int totalLatenessCount;
    public int sickDaysCount;
    
    // Особенность (Trait)
    public StaffController.TraitType trait;
}

[System.Serializable]
public struct DocumentStackSaveData
{
    public string stackOwnerName;
    public int documentCount;
}

// [НОВОЕ] Структура для сохранения прочности
[System.Serializable]
public struct DurabilitySaveData
{
    public string objectName;   // Уникальное имя объекта на сцене
    public Vector3 position;    // Позиция для доп. проверки
    public float currentHealth; // Текущее здоровье
}