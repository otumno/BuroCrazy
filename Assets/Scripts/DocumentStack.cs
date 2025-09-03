// Файл: DocumentStack.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DocumentStack : MonoBehaviour
{
    [Header("Настройки стопки")]
    [Tooltip("Максимальное количество документов в стопке.")]
    public int maxStackSize = 10;
    [Tooltip("Префаб одного документа для визуализации стопки.")]
    public GameObject documentVisualPrefab;
    [Tooltip("Вертикальное смещение для каждого нового документа в стопке.")]
    public float stackOffset = 0.05f;
    private List<GameObject> visualStack = new List<GameObject>();
    public int CurrentSize => visualStack.Count;
    public bool IsFull => CurrentSize >= maxStackSize;
    public bool IsEmpty => CurrentSize == 0;

    public void AddDocumentToStack()
    {
        if (IsFull)
        {
            return;
        }
        
        if (documentVisualPrefab == null)
        {
            Debug.LogError($"<color=red>[{name}] НЕ МОЖЕТ создать копию документа, потому что в инспекторе не назначен 'Document Visual Prefab'!</color>");
            return;
        }

        Vector3 position = transform.position + new Vector3(0, CurrentSize * stackOffset, 0);
        GameObject newDoc = Instantiate(documentVisualPrefab, position, transform.rotation, transform);
        visualStack.Add(newDoc);
    }

    public int TakeEntireStack()
    {
        int count = CurrentSize;
        foreach (var doc in visualStack)
        {
            Destroy(doc);
        }
        visualStack.Clear();
        return count;
    }

    public bool TakeOneDocument()
    {
        if (IsEmpty) return false;

        GameObject docToRemove = visualStack.Last();
        visualStack.Remove(docToRemove);
        Destroy(docToRemove);
        return true;
    }
}