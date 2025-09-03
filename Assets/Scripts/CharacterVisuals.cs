// Файл: CharacterVisuals.cs
using UnityEngine;

public class CharacterVisuals : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    public SpriteRenderer bodyRenderer;
    public SpriteRenderer faceRenderer;
    
    [Header("Данные")]
    public EmotionSpriteCollection spriteCollection;
    
    // --- НОВОЕ: Ссылка на карту эмоций ---
    [Tooltip("Перетащите сюда ассет с картой состояний и эмоций для этого типа персонажа")]
    public StateEmotionMap stateEmotionMap;

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

    public void Setup(Gender gender)
    {
        if (spriteCollection == null)
        {
            Debug.LogError("Sprite Collection не назначен в CharacterVisuals!", gameObject);
            return;
        }

        this.characterGender = gender;
        bodyRenderer.sprite = spriteCollection.GetBodySprite(gender);
        SetEmotion(Emotion.Neutral);
    }

    // Старый метод для прямой установки эмоции
    public void SetEmotion(Emotion emotion)
    {
        if (spriteCollection == null) return;
        faceRenderer.sprite = spriteCollection.GetFaceSprite(emotion, characterGender);
    }

    // --- НОВЫЙ МЕТОД: Устанавливает эмоцию на основе состояния из карты ---
    public void SetEmotionForState(System.Enum state)
    {
        if (stateEmotionMap == null) return;

        // Ищем в карте эмоцию для текущего состояния
        if (stateEmotionMap.TryGetEmotionForState(state.ToString(), out Emotion emotionToShow))
        {
            SetEmotion(emotionToShow);
        }
        else // Если для такого состояния нет записи в карте, ставим нейтральную
        {
            SetEmotion(Emotion.Neutral);
        }
    }
}