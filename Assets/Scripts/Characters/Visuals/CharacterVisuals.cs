// Файл: Assets/Scripts/Characters/Visuals/CharacterVisuals.cs
using UnityEngine;
using System.Collections; // Добавлено для корутин

public class CharacterVisuals : MonoBehaviour
{
    // Новый enum для запроса точек
    public enum AttachPointType { Head, Hand }

    private SpriteRenderer bodyRenderer;
    private SpriteRenderer faceRenderer;
    private Transform headAttachPoint;
    private Transform handAttachPoint;
    private SpriteRenderer levelUpEffectRenderer; // Ссылка на рендерер эффекта
    private Coroutine levelUpCoroutine; // Для управления корутиной эффекта

    private EmotionSpriteCollection currentSpriteCollection;
    private StateEmotionMap currentStateEmotionMap;
    private Gender characterGender;
    private ClientNotification clientNotification; // Оставил, хотя может быть не используется здесь
	
	[Header("Звуки")] 
    [Tooltip("Звук, проигрываемый при повышении уровня")]
    public AudioClip levelUpSound;

    void Awake()
    {
        StaffPrefabReferences references = GetComponent<StaffPrefabReferences>();
        if (references != null)
        {
            this.bodyRenderer = references.bodyRenderer;
            this.faceRenderer = references.faceRenderer;
            this.headAttachPoint = references.headAttachPoint;
            this.handAttachPoint = references.handAttachPoint;
            // Получаем ссылку на рендерер эффекта повышения уровня
            this.levelUpEffectRenderer = references.levelUpEffectRenderer;
        }
        // Выключаем рендерер эффекта при старте на всякий случай
        if (levelUpEffectRenderer != null)
        {
            levelUpEffectRenderer.enabled = false;
        }
        else
        {
             // Если ссылка не найдена через StaffPrefabReferences, попробуем найти по имени (менее надежно)
             Transform effectTransform = transform.Find("LevelUpEffectSprite"); // Имя должно совпадать с тем, что в префабе
             if (effectTransform != null) {
                 levelUpEffectRenderer = effectTransform.GetComponent<SpriteRenderer>();
                 if (levelUpEffectRenderer != null) levelUpEffectRenderer.enabled = false;
                 else Debug.LogWarning("Найден LevelUpEffectSprite, но на нем нет SpriteRenderer!", gameObject);
             } else {
                 Debug.LogWarning("LevelUpEffectRenderer не назначен через StaffPrefabReferences и не найден по имени!", gameObject);
             }
        }
    }

    /// <summary>
    /// Возвращает Transform указанной точки крепления.
    /// </summary>
    public Transform GetAttachPoint(AttachPointType type)
    {
        switch (type)
        {
            case AttachPointType.Head:
                return headAttachPoint;
            case AttachPointType.Hand:
                return handAttachPoint;
            default:
                 Debug.LogWarning($"Запрошена неизвестная точка крепления: {type}");
                return null;
        }
    }

    // Настройка внешнего вида из RoleData
    public void SetupFromRoleData(RoleData data, Gender gender)
    {
        if (data == null)
        {
             Debug.LogError("Попытка настроить CharacterVisuals с null RoleData!", gameObject);
             return;
        }
        Setup(gender, data.spriteCollection, data.stateEmotionMap); // Настраиваем тело и лицо
        EquipAccessory(data.accessoryPrefab); // Надеваем аксессуар
    }

    // Основная настройка тела и лица
    public void Setup(Gender gender, EmotionSpriteCollection collection, StateEmotionMap emotionMap)
    {
        this.characterGender = gender;
        this.currentSpriteCollection = collection;
        this.currentStateEmotionMap = emotionMap;

        if (currentSpriteCollection == null)
        {
            Debug.LogError("EmotionSpriteCollection не назначен для этого персонажа!", gameObject);
            // Можно установить дефолтные спрайты или оставить как есть
            return;
        }
        if (bodyRenderer == null)
        {
             Debug.LogError("BodyRenderer не назначен или не найден!", gameObject);
            return;
        }

        bodyRenderer.sprite = currentSpriteCollection.GetBodySprite(gender); // Устанавливаем спрайт тела
        SetEmotion(Emotion.Neutral); // Устанавливаем нейтральное лицо по умолчанию
    }

    // Устанавливает спрайт лица для указанной эмоции
    public void SetEmotion(Emotion emotion)
    {
        if (currentSpriteCollection == null || faceRenderer == null) return; // Проверка
        faceRenderer.sprite = currentSpriteCollection.GetFaceSprite(emotion, characterGender);
    }

    // Устанавливает эмоцию на основе текущего состояния персонажа (из Enum)
    public void SetEmotionForState(System.Enum state)
    {
        if (currentStateEmotionMap == null)
        {
             // Если карты состояний нет, просто ставим нейтральное лицо
             SetEmotion(Emotion.Neutral);
             return;
        }
        // Пытаемся найти эмоцию для строкового представления имени состояния
        if (currentStateEmotionMap.TryGetEmotionForState(state.ToString(), out Emotion emotionToShow))
        {
            SetEmotion(emotionToShow); // Если нашли - устанавливаем
        }
        else
        {
            SetEmotion(Emotion.Neutral); // Если не нашли - ставим нейтральное
             // Debug.LogWarning($"Эмоция для состояния '{state.ToString()}' не найдена в StateEmotionMap '{currentStateEmotionMap.name}'. Установлена Neutral.", gameObject);
        }
    }

    // Надевает аксессуар (например, кепку) на соответствующую точку крепления
	public void EquipAccessory(GameObject newAccessoryPrefab)
    {
         // Проверяем наличие точек крепления
        if (headAttachPoint == null && handAttachPoint == null)
        {
             Debug.LogWarning("Нет точек крепления (Head/Hand) для аксессуаров!", gameObject);
            return;
        }
        // Удаляем старые аксессуары
        if (headAttachPoint != null) {
            foreach (Transform child in headAttachPoint) { Destroy(child.gameObject); }
        }
        if (handAttachPoint != null) {
            foreach (Transform child in handAttachPoint) { Destroy(child.gameObject); }
        }

        // Если нет нового префаба, просто выходим
        if (newAccessoryPrefab == null) return;

        // Определяем, куда крепить: на голову (если имя содержит cap/hat) или на руку
        Transform targetAttachPoint = handAttachPoint; // По умолчанию на руку
         if (headAttachPoint != null && (newAccessoryPrefab.name.ToLower().Contains("cap") || newAccessoryPrefab.name.ToLower().Contains("hat")))
        {
            targetAttachPoint = headAttachPoint;
        }

        // Если подходящая точка крепления существует
        if (targetAttachPoint != null) {
            // Создаем экземпляр аксессуара как дочерний объект точки крепления
            GameObject accessoryInstance = Instantiate(newAccessoryPrefab, targetAttachPoint);
            accessoryInstance.transform.localPosition = Vector3.zero; // Сбрасываем позицию относительно родителя
            accessoryInstance.transform.localRotation = Quaternion.identity; // Сбрасываем поворот
            // Можно добавить настройку Sorting Layer/Order, если аксессуары перекрываются неправильно
        } else {
             Debug.LogWarning($"Не найдена подходящая точка крепления для аксессуара '{newAccessoryPrefab.name}'", gameObject);
        }
    }

    /// <summary>
    /// Запускает визуальный эффект повышения уровня.
    /// </summary>
    /// <param name="duration">Длительность эффекта в секундах.</param>
    public void PlayLevelUpEffect(float duration = 1.5f)
    {
		
		if (levelUpSound != null)
    {
        // AudioSource.PlayClipAtPoint создает временный AudioSource для проигрывания
        AudioSource.PlayClipAtPoint(levelUpSound, transform.position, 1.0f); // 1.0f - громкость
    }
		
        if (levelUpEffectRenderer == null)
        {
            // Debug.LogWarning("LevelUpEffectRenderer не назначен, эффект повышения не может быть показан.", gameObject); // Можно раскомментировать для отладки
            return;
        }

        // Если эффект уже проигрывается, останавливаем предыдущую корутину
        if (levelUpCoroutine != null)
        {
            StopCoroutine(levelUpCoroutine);
             // Убедимся, что рендерер выключен, если прервали анимацию
             levelUpEffectRenderer.enabled = false;
        }
        // Запускаем новую корутину
        levelUpCoroutine = StartCoroutine(LevelUpEffectRoutine(duration));
    }

    /// <summary>
    /// Корутина, управляющая отображением и анимацией эффекта повышения уровня.
    /// </summary>
    private IEnumerator LevelUpEffectRoutine(float duration)
    {
        if (levelUpEffectRenderer == null) yield break;

        levelUpEffectRenderer.enabled = true; // Включаем спрайт

        // Анимация плавного появления и исчезания
        float fadeDuration = duration / 2f; // Половина времени на появление, половина на исчезание
        Color startColor = levelUpEffectRenderer.color; // Запоминаем исходный цвет
        Color targetColorFull = startColor; // Полностью видимый цвет
        Color targetColorFade = startColor;
        targetColorFade.a = 0f; // Полностью прозрачный цвет

        // Fade In (Появление)
        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            levelUpEffectRenderer.color = Color.Lerp(targetColorFade, targetColorFull, timer / fadeDuration);
            yield return null; // Ждем следующего кадра
        }
        levelUpEffectRenderer.color = targetColorFull; // Убедимся, что альфа = 1

        // Fade Out (Исчезание)
        timer = 0f; // Сбрасываем таймер
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            levelUpEffectRenderer.color = Color.Lerp(targetColorFull, targetColorFade, timer / fadeDuration);
            yield return null; // Ждем следующего кадра
        }

        levelUpEffectRenderer.enabled = false; // Выключаем спрайт в конце
        levelUpEffectRenderer.color = startColor; // Возвращаем исходную альфу на случай следующего включения
        levelUpCoroutine = null; // Сбрасываем ссылку на корутину
    }
}