using UnityEngine;
using System.Collections.Generic;
using Gameplay.Documents;
using Data.Documents;
using Managers;

public class DocumentStack : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Максимальная высота стопки (визуально)")]
    public int maxStackSize = 20;
    public float stackOffset = 0.015f; 

    [Header("Ссылки")]
    [Tooltip("Префаб обычного белого документа")]
    public GameObject documentPrefab; 
    
    [Tooltip("Точка, где растет стопка")]
    public Transform stackRoot;

    // Внутренний список объектов
    private List<GameObject> visualStack = new List<GameObject>();

    // --- СВОЙСТВА ---
    public bool IsEmpty => visualStack.Count == 0;
    public bool IsFull => visualStack.Count >= maxStackSize; // << ВЕРНУЛИ ISFULL
    public int CurrentSize => visualStack.Count;

    private void Start()
    {
        if (stackRoot == null) stackRoot = transform;
    }

    // =================================================================================
    // МЕТОДЫ ДЛЯ ОБЫЧНЫХ ДОКУМЕНТОВ (БЕЛЫЕ)
    // =================================================================================

    /// <summary>
    /// Добавляет документ. Теперь возвращает bool (для WriteReportExecutor).
    /// </summary>
    public bool AddDocumentToStack()
    {
        if (visualStack.Count >= maxStackSize) return false;
        AddDocumentInternal(documentPrefab);
        return true;
    }

    /// <summary>
    /// Пытается забрать один документ сверху. Возвращает true, если успешно.
    /// </summary>
    public bool TakeOneDocument()
    {
        if (visualStack.Count > 0)
        {
            RemoveDocumentFromStack();
            return true;
        }
        return false;
    }

    public void RemoveDocumentFromStack()
    {
        if (visualStack.Count > 0)
        {
            GameObject topDoc = visualStack[visualStack.Count - 1];
            visualStack.RemoveAt(visualStack.Count - 1);
            if (topDoc != null) Destroy(topDoc);
        }
    }

    /// <summary>
    /// Забирает ВСЮ стопку и возвращает количество (для ArchiveManager/Director).
    /// </summary>
    public int TakeEntireStack()
    {
        int count = visualStack.Count;
        
        // Уничтожаем все визуальные объекты
        foreach (var doc in visualStack)
        {
            if (doc != null) Destroy(doc);
        }
        visualStack.Clear();
        
        return count;
    }

    /// <summary>
    /// Восстанавливает стопку при загрузке (для SaveLoadManager).
    /// </summary>
    public void SetCount(int count)
    {
        // Сначала очищаем
        TakeEntireStack();

        // Спавним нужное количество
        for (int i = 0; i < count; i++)
        {
            if (visualStack.Count >= maxStackSize) break;
            AddDocumentInternal(documentPrefab);
        }
    }

    // =================================================================================
    // ПРОЕКТНЫЕ ДОКУМЕНТЫ (КРАСНЫЕ ПАПКИ)
    // =================================================================================

    public bool AddProjectDocument(ProjectDocumentDefinition data, GameObject prefab)
    {
        if (visualStack.Count >= maxStackSize) return false;

        GameObject newDoc = AddDocumentInternal(prefab);
        if (newDoc != null)
        {
            var script = newDoc.GetComponent<ProjectDocumentObject>();
            if (script != null) script.Initialize(data);
            return true;
        }
        return false;
    }

    public ProjectDocumentObject FindPendingProjectDocument(StaffController.Role role)
    {
        if (visualStack == null || visualStack.Count == 0) return null;

        foreach (var docGO in visualStack)
        {
            if (docGO == null) continue;
            
            var projDoc = docGO.GetComponent<ProjectDocumentObject>();
            if (projDoc == null || projDoc.documentData == null) continue;

            var data = projDoc.documentData;

            // Логика фильтрации
            if (role == StaffController.Role.Registrar)
            {
                if (data.signedByDirector && !data.processedByRegistrar) return projDoc;
            }
            else if (role == StaffController.Role.Cashier || role == StaffController.Role.Accountant)
            {
                if (data.processedByRegistrar && !data.paidAtCashier) return projDoc;
            }
            else if (role == StaffController.Role.Archivist)
            {
                if (data.paidAtCashier && !data.archived) return projDoc;
            }
        }
        return null;
    }

    public ProjectDocumentObject FindPendingProjectDocument(ClerkController.ClerkRole clerkRole)
    {
        StaffController.Role role = StaffController.Role.Clerk;
        if (clerkRole == ClerkController.ClerkRole.Registrar) role = StaffController.Role.Registrar;
        if (clerkRole == ClerkController.ClerkRole.Cashier) role = StaffController.Role.Cashier;
        if (clerkRole == ClerkController.ClerkRole.Accountant) role = StaffController.Role.Accountant;
        
        return FindPendingProjectDocument(role);
    }

    public GameObject TakeSpecificDocument(ProjectDocumentObject targetDoc)
    {
        if (targetDoc == null || !visualStack.Contains(targetDoc.gameObject)) return null;
        visualStack.Remove(targetDoc.gameObject);
        return targetDoc.gameObject;
    }

    // =================================================================================
    // ВНУТРЕННЯЯ ЛОГИКА
    // =================================================================================

    private GameObject AddDocumentInternal(GameObject prefabToSpawn)
    {
        if (prefabToSpawn == null || stackRoot == null) return null;

        GameObject newDoc = Instantiate(prefabToSpawn, stackRoot);
        
        Vector3 pos = stackRoot.position + new Vector3(0, visualStack.Count * stackOffset, 0);
        Quaternion rot = stackRoot.rotation * Quaternion.Euler(0, 0, Random.Range(-5f, 5f));

        newDoc.transform.position = pos;
        newDoc.transform.rotation = rot;

        // Fix: Set sorting order to be above desk textures
        SpriteRenderer[] renderers = newDoc.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            if (sr != null) sr.sortingOrder = 50 + visualStack.Count;
        }
        Debug.Log($"[DocumentStack] Бумага добавлена на {gameObject.name}. Всего: {visualStack.Count}");

        visualStack.Add(newDoc);
        return newDoc;
    }
    
    public List<ProjectDocumentObject> GetAllProjectDocuments()
    {
        List<ProjectDocumentObject> list = new List<ProjectDocumentObject>();
        foreach(var go in visualStack)
        {
            if(go != null)
            {
                var comp = go.GetComponent<ProjectDocumentObject>();
                if(comp != null) list.Add(comp);
            }
        }
        return list;
    }
}