// Assets/Scripts/BuroDebug/DebugSpawner.cs
using UnityEngine;
using Managers;
using Data.Documents;
using Scriptables.Progression;

public class DebugSpawner : MonoBehaviour
{
    [Header("Куда спавнить")]
    public DocumentStack directorTableStack; // Перетащить стопку со стола Директора

    [Header("Что спавнить")]
    public GameObject redFolderPrefab; // Перетащить созданный префаб
    public RegionData regionData;      // Перетащить ассет региона "Slums"

    [ContextMenu("TEST: Spawn Region Doc")]
    public void SpawnRegionDoc()
    {
        if (directorTableStack == null || redFolderPrefab == null || regionData == null)
        {
            Debug.LogError("DebugSpawner: Заполните все поля!");
            return;
        }

        // 1. Создаем "душу" документа
        var docData = new ProjectDocumentDefinition(regionData);
        
        // 2. Кладем "тело" (префаб) с "душой" в стопку
        bool success = directorTableStack.AddProjectDocument(docData, redFolderPrefab);

        if (success) Debug.Log("Документ успешно создан на столе!");
        else Debug.Log("Не удалось создать документ (стопка полна?)");
    }
}