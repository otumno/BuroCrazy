// Файл: Assets/Scripts/Characters/Visuals/CharacterVisuals.cs
using UnityEngine;

public class CharacterVisuals : MonoBehaviour
{
    // Новый enum для запроса точек
    public enum AttachPointType { Head, Hand }

    private SpriteRenderer bodyRenderer;
    private SpriteRenderer faceRenderer;
    private Transform headAttachPoint;
    private Transform handAttachPoint;

    private EmotionSpriteCollection currentSpriteCollection;
    private StateEmotionMap currentStateEmotionMap;
    private Gender characterGender;
    private ClientNotification clientNotification;

    void Awake()
    {
        StaffPrefabReferences references = GetComponent<StaffPrefabReferences>();
        if (references != null)
        {
            this.bodyRenderer = references.bodyRenderer;
            this.faceRenderer = references.faceRenderer;
            this.headAttachPoint = references.headAttachPoint;
            this.handAttachPoint = references.handAttachPoint;
        }
    }

    // ----- НОВЫЙ ПУБЛИЧНЫЙ МЕТОД -----
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
                return null;
        }
    }
    // ----- КОНЕЦ НОВОГО МЕТОДА -----


    // ... (остальная часть скрипта остается без изменений) ...
    public void SetupFromRoleData(RoleData data, Gender gender)
    {
        if (data == null) return;
        Setup(gender, data.spriteCollection, data.stateEmotionMap);
        EquipAccessory(data.accessoryPrefab);
    }

    public void Setup(Gender gender, EmotionSpriteCollection collection, StateEmotionMap emotionMap)
    {
        this.characterGender = gender;
        this.currentSpriteCollection = collection;
        this.currentStateEmotionMap = emotionMap;

        if (currentSpriteCollection == null) return;
        if (bodyRenderer == null) { return; } 
        
        bodyRenderer.sprite = currentSpriteCollection.GetBodySprite(gender);
        SetEmotion(Emotion.Neutral);
    }

    public void SetEmotion(Emotion emotion)
    {
        if (currentSpriteCollection == null || faceRenderer == null) return;
        faceRenderer.sprite = currentSpriteCollection.GetFaceSprite(emotion, characterGender);
    }

    public void SetEmotionForState(System.Enum state)
    {
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
        if (headAttachPoint == null || handAttachPoint == null)
        {
            return;
        }
        foreach (Transform child in headAttachPoint) { Destroy(child.gameObject); }
        foreach (Transform child in handAttachPoint) { Destroy(child.gameObject); }
        if (newAccessoryPrefab == null) return;
        Transform targetAttachPoint = handAttachPoint;
        if (newAccessoryPrefab.name.ToLower().Contains("cap") || newAccessoryPrefab.name.ToLower().Contains("hat"))
        {
            targetAttachPoint = headAttachPoint;
        }
        GameObject accessoryInstance = Instantiate(newAccessoryPrefab, targetAttachPoint);
        accessoryInstance.transform.localPosition = Vector3.zero;
        accessoryInstance.transform.localRotation = Quaternion.identity;
    }
}