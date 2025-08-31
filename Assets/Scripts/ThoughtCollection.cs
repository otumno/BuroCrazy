// Файл: ThoughtCollection.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ThoughtCollection_Tiered", menuName = "My Game/Thought Collection (Tiered)")]
public class ThoughtCollection : ScriptableObject
{
    // Класс для хранения мыслей, разделенных по уровням "накала"
    [System.Serializable]
    public class ThoughtTiers
    {
        [Header(">= 66% (Зеленый уровень)")]
        [Tooltip("Спокойные, оптимистичные мысли")]
        [TextArea(1, 3)] public List<string> greenTierTexts;

        [Header("33-66% (Желтый уровень)")]
        [Tooltip("Напряженные, подозрительные, саркастичные мысли")]
        [TextArea(1, 3)] public List<string> yellowTierTexts;

        [Header("< 33% (Оранжевый уровень)")]
        [Tooltip("Раздраженные, близкие к панике мысли")]
        [TextArea(1, 3)] public List<string> orangeTierTexts;

        [Header("0% (Красный уровень / Ярость)")]
        [Tooltip("Мысли в ярости. Будут показаны красным и КАПСОМ.")]
        [TextArea(1, 3)] public List<string> redTierTexts;
    }
    
    // Класс, связывающий ключ деятельности с набором мыслей
    [System.Serializable]
    public class ActivityThought
    {
        [Tooltip("Ключ деятельности, например, 'Client_Waiting' или 'Guard_Patrolling'")]
        public string activityKey;
        public ThoughtTiers thoughts;
    }

    public List<ActivityThought> allThoughts;

    private Dictionary<string, ThoughtTiers> thoughtDictionary;

    public void Initialize()
    {
        if (allThoughts == null) return;
        thoughtDictionary = new Dictionary<string, ThoughtTiers>();
        foreach (var activity in allThoughts)
        {
            if (!thoughtDictionary.ContainsKey(activity.activityKey))
            {
                thoughtDictionary[activity.activityKey] = activity.thoughts;
            }
        }
    }

    /// <summary>
    /// Возвращает случайную мысль для деятельности, учитывая уровень параметра (терпение/стресс).
    /// </summary>
    /// <param name="activityKey">Ключ деятельности ("Client_Waiting")</param>
    /// <param name="parameterValue">Значение параметра от 0.0 (плохо) до 1.0 (хорошо)</param>
    /// <returns>Подходящий текст мысли.</returns>
    public string GetRandomThought(string activityKey, float parameterValue)
    {
        if (thoughtDictionary == null) Initialize();

        if (thoughtDictionary.TryGetValue(activityKey, out ThoughtTiers tiers))
        {
            List<string> listToUse = null;

            if (parameterValue <= 0.0f) listToUse = tiers.redTierTexts;
            else if (parameterValue < 0.33f) listToUse = tiers.orangeTierTexts;
            else if (parameterValue < 0.66f) listToUse = tiers.yellowTierTexts;
            else listToUse = tiers.greenTierTexts;

            if (listToUse != null && listToUse.Count > 0)
            {
                return listToUse[Random.Range(0, listToUse.Count)];
            }
        }
        return null;
    }
}