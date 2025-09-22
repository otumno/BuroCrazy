// Файл: DocumentManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Отслеживает общее количество необработанных документов на всех столах и в архиве.
/// </summary>
public class DocumentManager : MonoBehaviour
{
    public static DocumentManager Instance { get; private set; }

    [Header("Общие настройки")]
    [Tooltip("Максимально возможное количество документов в общем на всех столах и в архиве. По достижении этого значения UI будет подсвечен.")]
    public int maxTotalDocuments = 100;

    private List<DocumentStack> trackedStacks = new List<DocumentStack>();
    private float updateTimer = 0f;
    private const float UPDATE_INTERVAL = 0.5f; // Как часто обновляем счетчик (для оптимизации)
    
    public int CurrentTotalDocuments { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        // Находим все стопки документов на сцене при старте
        trackedStacks = FindObjectsByType<DocumentStack>(FindObjectsSortMode.None).ToList();
        UpdateDocumentCount();
    }

    void Update()
    {
        // Обновляем счетчик не каждый кадр, а с небольшим интервалом
        updateTimer += Time.deltaTime;
        if (updateTimer >= UPDATE_INTERVAL)
        {
            updateTimer = 0f;
            UpdateDocumentCount();
        }
    }

    /// <summary>
    /// Пересчитывает общее количество документов.
    /// </summary>
    private void UpdateDocumentCount()
    {
        // Убираем из списка уничтоженные стопки, если таковые имеются
        trackedStacks.RemoveAll(item => item == null);
        
        CurrentTotalDocuments = 0;
        foreach (var stack in trackedStacks)
        {
            CurrentTotalDocuments += stack.CurrentSize;
        }
    }
    
    /// <summary>
    /// Возвращает текстовое описание текущей ситуации с документами.
    /// </summary>
    public string GetDocumentStatusText()
    {
        if (CurrentTotalDocuments == 0) return "Пустота";

        float ratio = (float)CurrentTotalDocuments / maxTotalDocuments;

        if (ratio >= 1.0f) return "Лавина!";
        if (ratio >= 0.9f) return "Критический завал";
        if (ratio >= 0.75f) return "Переполнение";
        if (ratio >= 0.5f) return "Завал";
        if (ratio >= 0.25f) return "Накопление";
        
        return "Порядок";
    }
}