// Файл: EmotionSpriteCollection.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "EmotionCollection", menuName = "My Game/Emotion Sprite Collection")]
public class EmotionSpriteCollection : ScriptableObject
{
    // Класс для хранения спрайтов лица для одной эмоции
    [System.Serializable]
    public class EmotionEntry
    {
        public Emotion emotion; // Тип эмоции
        public Sprite maleFace; // Спрайт лица для мужского персонажа
        public Sprite femaleFace; // Спрайт лица для женского персонажа
    }

    // --- Структура для тела и его анимаций ходьбы ---
    [System.Serializable]
    public class BodyAnimationSet
    {
        [Tooltip("Базовый спрайт тела (состояние покоя)")]
        public Sprite idleBody;
        [Tooltip("Первый спрайт ходьбы для этого тела")]
        public Sprite walkBody1;
        [Tooltip("Второй спрайт ходьбы для этого тела")]
        public Sprite walkBody2;
    }
    // --- Конец структуры ---

    [Header("Наборы спрайтов тела с анимациями")]
    [Tooltip("Список вариантов для мужских персонажей. Каждый элемент содержит idle, walk1, walk2 спрайты.")]
    public List<BodyAnimationSet> maleBodySets; // Замена maleBaseBodies
    [Tooltip("Список вариантов для женских персонажей. Каждый элемент содержит idle, walk1, walk2 спрайты.")]
    public List<BodyAnimationSet> femaleBodySets; // Замена femaleBaseBodies

    [Header("Спрайты эмоций для лиц")]
    [Tooltip("Список сопоставлений эмоций и спрайтов лиц для каждого пола.")]
    public List<EmotionEntry> emotionFaces;

    /// <summary>
    /// Возвращает спрайт лица для указанной эмоции и пола.
    /// Если спрайт для запрошенной эмоции не найден, пытается вернуть нейтральный спрайт.
    /// </summary>
    /// <param name="emotion">Требуемая эмоция.</param>
    /// <param name="gender">Пол персонажа.</param>
    /// <returns>Спрайт лица или null, если ничего не найдено.</returns>
    public Sprite GetFaceSprite(Emotion emotion, Gender gender)
    {
        // Проверяем наличие списка эмоций
        if (emotionFaces == null || emotionFaces.Count == 0)
        {
            Debug.LogError($"Список emotionFaces пуст или не назначен в {this.name}!", this);
            return null;
        }

        // 1. Пытаемся найти запрошенную эмоцию
        var entry = emotionFaces.FirstOrDefault(e => e != null && e.emotion == emotion);
        Sprite targetSprite = null;

        if (entry != null)
        {
            // Выбираем спрайт для нужного пола
            targetSprite = (gender == Gender.Male) ? entry.maleFace : entry.femaleFace;
        }

        // 2. Если спрайт для запрошенной эмоции не найден (либо нет записи, либо в ней пустой слот)
        //    ИЛИ если запрошенная эмоция не была нейтральной (чтобы не искать нейтральную рекурсивно)
        if (targetSprite == null && emotion != Emotion.Neutral)
        {
            // Выводим предупреждение в консоль
            Debug.LogWarning($"Спрайт для эмоции '{emotion}' и пола '{gender}' не найден или не назначен в {this.name}! Используется нейтральная эмоция.", this);
            // Пытаемся найти нейтральную эмоцию в качестве запасного варианта
            var neutralEntry = emotionFaces.FirstOrDefault(e => e != null && e.emotion == Emotion.Neutral);
            if (neutralEntry != null)
            {
                // Выбираем нейтральный спрайт для нужного пола
                targetSprite = (gender == Gender.Male) ? neutralEntry.maleFace : neutralEntry.femaleFace;
                if (targetSprite == null) {
                    Debug.LogError($"Даже нейтральный спрайт для пола '{gender}' не назначен в {this.name}!", this);
                }
            }
            else
            {
                Debug.LogError($"Запись для нейтральной эмоции (Emotion.Neutral) не найдена в {this.name}!", this);
            }
        }
         else if (targetSprite == null && emotion == Emotion.Neutral) {
             Debug.LogError($"Нейтральный спрайт для пола '{gender}' не назначен в {this.name}!", this);
         }


        return targetSprite; // Возвращаем найденный спрайт (или null, если ничего не найдено)
    }

    /// <summary>
    /// Возвращает случайный набор спрайтов тела (idle, walk1, walk2) для указанного пола.
    /// </summary>
    /// <param name="gender">Пол персонажа.</param>
    /// <returns>Случайный BodyAnimationSet или null, если наборы не найдены.</returns>
    public BodyAnimationSet GetRandomBodySet(Gender gender)
    {
        // Выбираем нужный список наборов (мужской или женский)
        List<BodyAnimationSet> targetList = (gender == Gender.Male) ? maleBodySets : femaleBodySets;

        // Проверяем, что список существует и не пуст
        if (targetList != null && targetList.Count > 0)
        {
            // Убираем null элементы из списка перед выбором
            var validSets = targetList.Where(s => s != null && s.idleBody != null && s.walkBody1 != null && s.walkBody2 != null).ToList();
            if (validSets.Count > 0) {
                // Возвращаем случайный ВАЛИДНЫЙ элемент из списка
                return validSets[Random.Range(0, validSets.Count)];
            } else {
                 Debug.LogError($"Не найдены валидные (полностью заполненные) BodyAnimationSet для пола {gender} в коллекции {this.name}. Список targetList содержит {targetList.Count} элементов, но все они неполные или null.", this);
                 return null;
            }

        }

        // Если список пуст или не назначен, выводим ошибку и возвращаем null
        Debug.LogError($"Не найдены или не назначены BodyAnimationSet для пола {gender} в коллекции {this.name}!", this);
        return null;
    }

} // Конец класса EmotionSpriteCollection