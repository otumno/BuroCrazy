// Файл: DocumentStack.cs
using UnityEngine;
using System.Collections.Generic;

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

    // Добавляет один документ в стопку
    public void AddDocumentToStack()
    {
        if (IsFull || documentVisualPrefab == null) return;

        Vector3 position = transform.position + new Vector3(0, CurrentSize * stackOffset, 0);
        GameObject newDoc = Instantiate(documentVisualPrefab, position, transform.rotation, transform);
        visualStack.Add(newDoc);
    }

    // Забирает всю стопку (возвращает количество документов)
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
}