using System.Collections.Generic; // Необходимо для работы со списками
using UnityEngine; // Необходимо для Vector3

[System.Serializable]
public class SaveData
{
    // Глобальные данные
    public int day;
    public int money;
    public int archiveDocumentCount;

    // Списки для хранения данных об отдельных объектах
    public List<StaffSaveData> allStaffData;
    public List<DocumentStackSaveData> allDocumentStackData;
}

// Отдельная структура для хранения данных одного сотрудника
[System.Serializable]
public struct StaffSaveData
{
    public string characterName; // Уникальное имя объекта, например "Clerk_Registrar"
    public float stressLevel;
    public Vector3 position;
}

// Отдельная структура для хранения данных одной стопки документов
[System.Serializable]
public struct DocumentStackSaveData
{
    public string stackOwnerName; // Уникальное имя объекта, на котором лежит стопка
    public int documentCount;
}