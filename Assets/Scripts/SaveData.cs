using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SaveData
{
    // Глобальные данные
    public int day;
    public int money;
    public int archiveDocumentCount;

    // --- НОВЫЕ ПОЛЯ ДЛЯ ПРИКАЗОВ ---
    public List<string> activePermanentOrderNames;
    public List<string> completedOneTimeOrderNames;
    // ------------------------------------

    // Списки для хранения данных об отдельных объектах
    public List<StaffSaveData> allStaffData;
    public List<DocumentStackSaveData> allDocumentStackData;
}

[System.Serializable]
public struct StaffSaveData
{
    public string characterName;
    public float stressLevel;
    public Vector3 position;
}

[System.Serializable]
public struct DocumentStackSaveData
{
    public string stackOwnerName;
    public int documentCount;
}