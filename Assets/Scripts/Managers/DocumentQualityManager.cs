using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class DocumentQualityManager : MonoBehaviour
{
    public static DocumentQualityManager Instance { get; private set; }

    // Храним качество каждого обработанного за день документа (1.0 = идеально, 0.0 = ужасно)
    private List<float> dailyDocumentQualityRatings = new List<float>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); } else { Instance = this; }
    }

    /// <summary>
    /// Регистрирует обработанный документ в системе.
    /// </summary>
    public void RegisterProcessedDocument(float documentQuality)
    {
        dailyDocumentQualityRatings.Add(documentQuality);
    }

    /// <summary>
    /// Возвращает средний ПРОЦЕНТ ОШИБОК за день (от 0.0 до 1.0).
    /// </summary>
    public float GetCurrentAverageErrorRate()
    {
        if (dailyDocumentQualityRatings.Count == 0) return 0f;
        // Считаем среднее качество, а затем вычитаем его из 1, чтобы получить % ошибок
        return 1f - dailyDocumentQualityRatings.Average();
    }

    /// <summary>
    /// Архивариус находит документ с наибольшим количеством ошибок и исправляет его.
    /// </summary>
    /// <returns>Возвращает true, если была найдена и исправлена ошибка.</returns>
    public bool CorrectWorstDocument(float correctionStrength)
    {
        if (dailyDocumentQualityRatings.Count == 0) return false;

        float worstQuality = 1.0f;
        int worstIndex = -1;

        // Ищем документ с самым низким качеством (т.е. с макс. ошибками)
        for(int i = 0; i < dailyDocumentQualityRatings.Count; i++)
        {
            if(dailyDocumentQualityRatings[i] < worstQuality)
            {
                worstQuality = dailyDocumentQualityRatings[i];
                worstIndex = i;
            }
        }

        // Если найден документ с ошибками
        if (worstIndex != -1 && worstQuality < 1.0f)
        {
            // Исправляем его качество, но не выше 1.0
            dailyDocumentQualityRatings[worstIndex] = Mathf.Min(1.0f, worstQuality + correctionStrength);
            return true;
        }

        return false; // Ошибок для исправления нет
    }

    /// <summary>
    /// Сбрасывает все счетчики для нового дня.
    /// </summary>
    public void ResetDay()
    {
        dailyDocumentQualityRatings.Clear();
    }
}