// Assets/Scripts/Data/SaveData.cs
using System.Collections.Generic;
using UnityEngine;
using Managers; // Для доступа к Enum ролей

[System.Serializable]
public class SaveData
{
    // --- Глобальные данные ---
    public int day;
    public int money;
    public int archiveDocumentCount;
    
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

    public HashSet<string> watchedDialogues;
}

[System.Serializable]
public struct StaffSaveData
{
    public string characterName;
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