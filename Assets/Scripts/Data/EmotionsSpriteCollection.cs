// Файл: EmotionSpriteCollection.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "EmotionCollection", menuName = "My Game/Emotion Sprite Collection")]
public class EmotionSpriteCollection : ScriptableObject
{
    [System.Serializable]
    public class EmotionEntry
    {
        public Emotion emotion;
        public Sprite maleFace;
        public Sprite femaleFace;
    }

    [Header("Базовые спрайты тела")]
    [Tooltip("Список вариантов мужских тел. При спавне будет выбрано случайное.")]
    public List<Sprite> maleBaseBodies;
    [Tooltip("Список вариантов женских тел. При спавне будет выбрано случайное.")]
    public List<Sprite> femaleBaseBodies;
    
    [Header("Спрайты эмоций для лиц")]
    public List<EmotionEntry> emotionFaces;

    public Sprite GetFaceSprite(Emotion emotion, Gender gender)
    {
        // --- ЛОГИКА ИЗМЕНЕНА ДЛЯ НАДЕЖНОСТИ ---

        // 1. Пытаемся найти запрошенную эмоцию
        var entry = emotionFaces.FirstOrDefault(e => e.emotion == emotion);
        Sprite targetSprite = null;

        if (entry != null)
        {
            targetSprite = (gender == Gender.Male) ? entry.maleFace : entry.femaleFace;
        }

        // 2. Если спрайт не найден (либо нет записи, либо в ней пустой слот)
        if (targetSprite == null && emotion != Emotion.Neutral)
        {
            // Выводим в консоль предупреждение, чтобы вы знали, какой спрайт забыли добавить
            Debug.LogWarning($"Спрайт для эмоции '{emotion}' и пола '{gender}' не найден! Используется нейтральная эмоция.", this);
            
            // Ищем нейтральную эмоцию в качестве запасного варианта
            var neutralEntry = emotionFaces.FirstOrDefault(e => e.emotion == Emotion.Neutral);
            if (neutralEntry != null)
            {
                targetSprite = (gender == Gender.Male) ? neutralEntry.maleFace : neutralEntry.femaleFace;
            }
        }
        
        return targetSprite;
    }
    
    public Sprite GetBodySprite(Gender gender)
    {
        if (gender == Gender.Male)
        {
            if (maleBaseBodies != null && maleBaseBodies.Count > 0)
            {
                return maleBaseBodies[Random.Range(0, maleBaseBodies.Count)];
            }
        }
        else
        {
            if (femaleBaseBodies != null && femaleBaseBodies.Count > 0)
            {
                return femaleBaseBodies[Random.Range(0, femaleBaseBodies.Count)];
            }
        }
        
        return null;
    }
}