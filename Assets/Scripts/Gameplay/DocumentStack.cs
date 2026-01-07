// Assets/Scripts/Gameplay/DocumentStack.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Gameplay.Documents; // Наш новый namespace
using Data.Documents;     // Данные документов

public class DocumentStack : MonoBehaviour
{
    [Header("Настройки стопки")]
    public int maxStackSize = 10;
    
    [Tooltip("Префаб для ОБЫЧНЫХ клиентских документов")]
    public GameObject documentVisualPrefab;
    
    [Tooltip("Смещение по высоте")]
    public float stackOffset = 0.05f;
    
    // Список созданных объектов (визуал)
    private List<GameObject> visualStack = new List<GameObject>();

    public int CurrentSize => visualStack.Count;
    public bool IsFull => CurrentSize >= maxStackSize;
    public bool IsEmpty => CurrentSize == 0;

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ---

    /// <summary>
    /// Добавить обычный документ (визуальная пустышка).
    /// </summary>
    public bool AddDocumentToStack() 
    {
        return AddDocumentInternal(null, null);
    }

    /// <summary>
    /// Добавить ВАЖНЫЙ документ (с данными и своим префабом).
    /// </summary>
    public bool AddProjectDocument(ProjectDocumentDefinition projectDoc, GameObject prefabOverride)
    {
        if (projectDoc == null) return false;
        // Передаем данные и префаб во внутренний метод
        return AddDocumentInternal(prefabOverride, projectDoc);
    }

    /// <summary>
    /// Забрать верхний документ. 
    /// Возвращает данные (если это спец. документ) или null (если обычный).
    /// out documentObject - ссылка на физический объект, который нужно взять в руку.
    /// </summary>
    public ProjectDocumentDefinition TakeTopDocument(out GameObject documentObject)
    {
        if (IsEmpty) 
        {
            documentObject = null;
            return null;
        }
        
        // 1. Берем верхний объект
        GameObject docToRemove = visualStack.Last();
        visualStack.Remove(docToRemove);
        
        documentObject = docToRemove; // Передаем объект наружу (для Parent к руке)

        // 2. Проверяем, есть ли на нем данные
        var projComp = docToRemove.GetComponent<ProjectDocumentObject>();
        if (projComp != null)
        {
            return projComp.documentData;
        }

        return null; // Обычная бумага
    }

    // --- ВНУТРЕННЯЯ ЛОГИКА ---

    private bool AddDocumentInternal(GameObject specificPrefab, ProjectDocumentDefinition dataForInit)
    {
        if (IsFull) return false;

        GameObject prefabToUse = specificPrefab != null ? specificPrefab : documentVisualPrefab;

        if (prefabToUse == null)
        {
            Debug.LogError($"<color=red>[{name}] ОШИБКА: Не назначен префаб документа!</color>");
            return false;
        }

        Vector3 position = transform.position + new Vector3(0, CurrentSize * stackOffset, 0);
        
        // Легкий рандом вращения для реализма
        Quaternion rotation = transform.rotation * Quaternion.Euler(0, 0, Random.Range(-5f, 5f));

        GameObject newDocGO = Instantiate(prefabToUse, position, rotation, transform);
        
        // ИНИЦИАЛИЗАЦИЯ ДАННЫХ (Ключевой момент)
        if (dataForInit != null)
        {
            var projComp = newDocGO.GetComponent<ProjectDocumentObject>();
            if (projComp != null)
            {
                projComp.Initialize(dataForInit);
            }
            else
            {
                Debug.LogWarning($"На префабе {prefabToUse.name} нет скрипта ProjectDocumentObject, хотя переданы данные!");
            }
        }

        visualStack.Add(newDocGO);
        return true;
    }

    // --- МЕТОДЫ ДЛЯ СОВМЕСТИМОСТИ И ОЧИСТКИ ---

    public int TakeEntireStack()
    {
        int count = CurrentSize;
        foreach (var doc in visualStack) Destroy(doc);
        visualStack.Clear();
        return count;
    }

    public bool TakeOneDocument()
    {
        // Упрощенный метод для уничтожения (если берет не игрок, а система/скрипт)
        if (IsEmpty) return false;
        GameObject doc;
        TakeTopDocument(out doc);
        if (doc != null) Destroy(doc);
        return true;
    }

    public void SetCount(int count)
    {
        TakeEntireStack();
        for (int i = 0; i < count; i++) AddDocumentToStack();
    }
}