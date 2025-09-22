// Файл: CharacterVisuals.cs
using UnityEngine;

public class CharacterVisuals : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    public SpriteRenderer bodyRenderer;
    public SpriteRenderer faceRenderer;
	
	[Header("Точки крепления")]
    public Transform headAttachPoint;
    public Transform handAttachPoint;

    private EmotionSpriteCollection currentSpriteCollection;
    private StateEmotionMap currentStateEmotionMap;
    private Gender characterGender;
    private ClientNotification clientNotification;

    void Awake()
    {
        clientNotification = GetComponent<ClientNotification>();
        if (clientNotification != null)
        {
            clientNotification.enabled = false;
        }
    }

    public void Setup(Gender gender, EmotionSpriteCollection collection, StateEmotionMap emotionMap)
    {
        // Сохраняем полученные данные
        this.characterGender = gender;
        this.currentSpriteCollection = collection;
        this.currentStateEmotionMap = emotionMap;

        if (currentSpriteCollection == null)
        {
            Debug.LogError("Sprite Collection не был передан в CharacterVisuals!", gameObject);
            return;
        }
        
        // Устанавливаем тело и нейтральное лицо
        bodyRenderer.sprite = currentSpriteCollection.GetBodySprite(gender);
        SetEmotion(Emotion.Neutral);
    }

    // Старый метод для прямой установки эмоции
    public void SetEmotion(Emotion emotion)
    {
        if (currentSpriteCollection == null) return;
        faceRenderer.sprite = currentSpriteCollection.GetFaceSprite(emotion, characterGender);
    }

    // Новый метод: Устанавливает эмоцию на основе состояния из карты
    public void SetEmotionForState(System.Enum state)
    {
        // Используем сохраненную карту эмоций
        if (currentStateEmotionMap == null) return;
        
        if (currentStateEmotionMap.TryGetEmotionForState(state.ToString(), out Emotion emotionToShow))
        {
            SetEmotion(emotionToShow);
        }
        else
        {
            SetEmotion(Emotion.Neutral);
        }
    }
	
	public void EquipAccessory(GameObject newAccessoryPrefab)
    {
        // Шаг 1: Удаляем все старые аксессуары
        foreach (Transform child in headAttachPoint) { Destroy(child.gameObject); }
        foreach (Transform child in handAttachPoint) { Destroy(child.gameObject); }

        // Шаг 2: Если нового аксессуара нет, выходим
        if (newAccessoryPrefab == null) return;

        // Шаг 3: Определяем, куда крепить новый аксессуар
        Transform targetAttachPoint = handAttachPoint; // По умолчанию крепим в руку
        if (newAccessoryPrefab.name.ToLower().Contains("cap") || newAccessoryPrefab.name.ToLower().Contains("hat"))
        {
            targetAttachPoint = headAttachPoint; // Если в имени есть "cap" или "hat", крепим на голову
        }

        // Шаг 4: Создаем и прикрепляем аксессуар
        GameObject accessoryInstance = Instantiate(newAccessoryPrefab, targetAttachPoint);
        accessoryInstance.transform.localPosition = Vector3.zero;
        accessoryInstance.transform.localRotation = Quaternion.identity;
    }
}