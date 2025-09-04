// Файл: ArchiveManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ArchiveManager : MonoBehaviour
{
    public static ArchiveManager Instance { get; private set; }

    [Header("Основная точка сдачи документов")]
    [Tooltip("Ссылка на стопку документов у стола архивариуса")]
    public DocumentStack mainDocumentStack;
    [Tooltip("Максимальное количество документов в основной стопке до того, как они начнут появляться в других местах")]
    public int maxCapacityBeforeOverflow = 20;
    [Header("Точки для переполнения")]
    [Tooltip("Список трансформов, где будут появляться документы, если основная стопка переполнена")]
    public List<Transform> overflowPoints;
    [Header("Архивные шкафы")]
    [Tooltip("Список всех шкафов, куда архивариус будет относить документы")]
    public List<ArchiveCabinet> cabinets;
    private List<Transform> occupiedOverflowPoints = new List<Transform>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }

    void Start()
    {
        if (cabinets == null || cabinets.Count == 0)
        {
            cabinets = FindObjectsByType<ArchiveCabinet>(FindObjectsSortMode.None).ToList();
        }
    }

    public Transform RequestDropOffPoint()
    {
        if (mainDocumentStack.CurrentSize < maxCapacityBeforeOverflow)
        {
            return mainDocumentStack.transform;
        }
        else
        {
            Transform freePoint = overflowPoints.FirstOrDefault(p => !occupiedOverflowPoints.Contains(p));
            if (freePoint != null)
            {
                occupiedOverflowPoints.Add(freePoint);
                return freePoint;
            }
        }
        
        Debug.LogWarning("Нет свободных мест для сдачи документов в архиве!");
        return null;
    }

    public DocumentStack GetStackToProcess()
    {
        return mainDocumentStack;
    }
    
    public ArchiveCabinet GetRandomCabinet()
    {
        if (cabinets == null || cabinets.Count == 0) return null;
        return cabinets[Random.Range(0, cabinets.Count)];
    }

    public void FreeOverflowPoint(Transform point)
    {
        if (occupiedOverflowPoints.Contains(point))
        {
            occupiedOverflowPoints.Remove(point);
        }
    }

    // --- НОВЫЕ МЕТОДЫ ДЛЯ СИСТЕМЫ СОХРАНЕНИЙ И НОВОЙ ИГРЫ ---

    public int GetCurrentDocumentCount()
    {
        if (mainDocumentStack == null) return 0;
        return mainDocumentStack.CurrentSize;
    }

    public void SetDocumentCount(int count)
    {
        if (mainDocumentStack == null) return;
        mainDocumentStack.TakeEntireStack(); // Очищаем стопку
        for (int i = 0; i < count; i++)
        {
            mainDocumentStack.AddDocumentToStack();
        }
    }

    public void ResetState()
    {
        if (mainDocumentStack != null)
        {
            mainDocumentStack.TakeEntireStack();
        }
        occupiedOverflowPoints.Clear();
    }
}