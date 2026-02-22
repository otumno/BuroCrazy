// Файл: Assets/Scripts/Characters/Visuals/CharacterVisuals.cs
using UnityEngine;
using System.Collections;
using Enums; // Required for Coroutines

namespace Characters
{
    public partial class CharacterVisuals : MonoBehaviour
    {
        // Enum to specify attachment points
        public enum AttachPointType { Head, Hand }

        // Component references (assigned via StaffPrefabReferences or fallback search)
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer faceRenderer;
    private Transform headAttachPoint;
    private Transform handAttachPoint;
    private SpriteRenderer levelUpEffectRenderer; // Renderer for the level-up effect sprite

    // Internal state
    internal EmotionSpriteCollection currentSpriteCollection;
    internal StateEmotionMap currentStateEmotionMap;
    internal Gender characterGender;
    private Coroutine levelUpCoroutine;

    private Sprite assignedPortrait;

    // Archetype tracking for Behavior Tree
    public Characters.ClientArchetype currentArchetype { get; private set; }

    // Flag to prevent visuals.Setup() from overwriting archetype body sprite
    private bool bodySpriteSetByArchetype = false;

    [Header("Звуки")] // Sound Effects section
    [Tooltip("Звук, проигрываемый при повышении уровня")]
    public AudioClip levelUpSound; // Sound clip for level up

    void Awake()
    {
        // Ищем Body и Face ВСЕГДА в VisualsContainer (игнорируем StaffPrefabReferences для них)
        // Это критически важно для правильной работы архетипов!
        Transform visualsContainer = transform.Find("VisualsContainer");

        // Body - ищем в VisualsContainer
        Transform bodyTF = visualsContainer != null ? visualsContainer.Find("Body") : transform.Find("Body");
        if (bodyTF != null)
        {
            bodyRenderer = bodyTF.GetComponent<SpriteRenderer>();
            if (bodyRenderer == null) Debug.LogError("Body found but has no SpriteRenderer!", gameObject);
        }
        else
        {
            Debug.LogError("Body not found in VisualsContainer!", gameObject);
        }

        // Face - ищем в VisualsContainer
        Transform faceTF = visualsContainer != null ? visualsContainer.Find("Face") : FindDeepChild(transform, "Face");
        if (faceTF != null)
        {
            faceRenderer = faceTF.GetComponent<SpriteRenderer>();
            if (faceRenderer == null) Debug.LogError("Face found but has no SpriteRenderer!", gameObject);
        }
        else
        {
            Debug.LogError("Face not found in VisualsContainer!", gameObject);
        }

        Debug.Log($"[CharacterVisuals] BodyRenderer: {(bodyRenderer != null ? bodyRenderer.gameObject.name : "NULL")}, FaceRenderer: {(faceRenderer != null ? faceRenderer.gameObject.name : "NULL")}");

        // Для остальных компонентов используем StaffPrefabReferences (Hand, HeadAttachPoint, etc)
        StaffPrefabReferences references = GetComponent<StaffPrefabReferences>();
        if (references != null)
        {
            this.headAttachPoint = references.headAttachPoint;
            this.handAttachPoint = references.handAttachPoint;
            this.levelUpEffectRenderer = references.levelUpEffectRenderer;
			this.levelUpSound = references.levelUpSound;
        }

         if (headAttachPoint == null) {
             Debug.LogWarning("HeadAttachPoint not found!", gameObject);
         }
         if (handAttachPoint == null) {
             Debug.LogWarning("HandAttachPoint not found!", gameObject);
         }


        // Fallback search specifically for the level up effect if not found via references
        if (levelUpEffectRenderer == null)
        {
            Transform effectTransform = transform.Find("LevelUpEffectSprite"); // Use the exact name of the GameObject
            if (effectTransform != null) {
                levelUpEffectRenderer = effectTransform.GetComponent<SpriteRenderer>();
                if (levelUpEffectRenderer == null) Debug.LogWarning("Найден LevelUpEffectSprite, но на нем нет SpriteRenderer!", gameObject);
            } else {
                 // Only log warning if references component also didn't provide it
                 if (references == null || references.levelUpEffectRenderer == null)
                    Debug.LogWarning("LevelUpEffectRenderer не назначен через StaffPrefabReferences и не найден по имени 'LevelUpEffectSprite'!", gameObject);
            }
        }

        // Ensure the level up effect is initially disabled
        if (levelUpEffectRenderer != null)
        {
            levelUpEffectRenderer.enabled = false;
        }
        // --- End Fallback Search & Initial State ---
    }

    /// <summary>
    /// Returns the Transform of the specified attachment point.
    /// </summary>
    public Transform GetAttachPoint(AttachPointType type)
    {
        switch (type)
        {
            case AttachPointType.Head:
                if (headAttachPoint == null) Debug.LogWarning($"Запрошена точка Head, но headAttachPoint не назначен/найден у {gameObject.name}");
                return headAttachPoint;
            case AttachPointType.Hand:
                 if (handAttachPoint == null) Debug.LogWarning($"Запрошена точка Hand, но handAttachPoint не назначен/найден у {gameObject.name}");
                return handAttachPoint;
            default:
                 Debug.LogError($"Запрошена неизвестная точка крепления: {type} у {gameObject.name}");
                return null;
        }
    }

    /// <summary>
    /// Sets up visuals based on RoleData and Gender.
    /// </summary>
    public void SetupFromRoleData(RoleData data, Gender gender)
    {
        if (data == null)
        {
             Debug.LogError($"Попытка настроить CharacterVisuals с null RoleData у {gameObject.name}!", gameObject);
             return;
        }
        // Call the main setup function with data from RoleData
        Setup(gender, data.spriteCollection, data.stateEmotionMap);
        // Equip the accessory defined in RoleData
        EquipAccessory(data.accessoryPrefab);
    }

    /// <summary>
    /// Main setup function for body sprite, face emotion, and animation sprites.
    /// </summary>
    public void Setup(Gender gender, EmotionSpriteCollection collection, StateEmotionMap emotionMap)
    {
        this.characterGender = gender;

        // Always set currentSpriteCollection - if archetype already set it, preserve it
        // Otherwise use the provided collection (or null if not provided)
        if (this.currentSpriteCollection == null && collection != null)
        {
            this.currentSpriteCollection = collection;
        }

        this.currentStateEmotionMap = emotionMap;

        // Validations
        if (currentSpriteCollection == null) {
            Debug.LogError($"EmotionSpriteCollection не назначен для {gameObject.name}! Внешний вид не будет настроен.", gameObject);
            return;
        }
        if (bodyRenderer == null) {
            Debug.LogError($"BodyRenderer не назначен или не найден у {gameObject.name}! Невозможно установить спрайт тела.", gameObject);
            return;
        }
        AgentMover agentMover = GetComponent<AgentMover>(); // Need AgentMover to set animation sprites
        if (agentMover == null) {
            Debug.LogError($"AgentMover не найден на {gameObject.name}! Невозможно настроить анимацию ходьбы.", gameObject);
            // Continue without animation setup if AgentMover is missing
        }

        // Get a random body animation set for the gender
        EmotionSpriteCollection.BodyAnimationSet bodySet = currentSpriteCollection.GetRandomBodySet(gender);

        // Check if body sprite was already set by archetype/SetupVisualDiversity
        bool bodySpriteAlreadySet = bodySpriteSetByArchetype;
        Sprite existingSprite = bodyRenderer.sprite;

        // Apply the body sprites if the set is valid
        if (bodySet != null && bodySet.idleBody != null && bodySet.walkBody1 != null && bodySet.walkBody2 != null)
        {
            if (!bodySpriteAlreadySet)
            {
                // Only set body sprite if not already set by archetype
                bodyRenderer.sprite = bodySet.idleBody;
                Debug.Log($"[{gameObject.name}] Setup: Using random body from collection: {bodySet.idleBody.name}");
            }
            else
            {
                Debug.Log($"[{gameObject.name}] Setup: Body sprite already set by archetype, keeping: {existingSprite?.name ?? "NULL"}");
            }

            // Provide all animation sprites to the AgentMover (use existing sprite if set, otherwise from bodySet)
            if (agentMover != null)
            {
                 Sprite idleSprite = bodySpriteAlreadySet && existingSprite != null ? existingSprite : bodySet.idleBody;
                 agentMover.SetAnimationSprites(idleSprite, bodySet.walkBody1, bodySet.walkBody2);
            }
			assignedPortrait = bodySet.portrait;
        }
        else // Handle cases where bodySet or its sprites are missing
        {
            Debug.LogError($"Не удалось получить валидный BodyAnimationSet для пола {gender} из коллекции {currentSpriteCollection?.name ?? "null"} у {gameObject.name}.", gameObject);
            // Try setting at least the idle sprite if available, otherwise body remains unchanged
             if (bodySet?.idleBody != null && !bodySpriteAlreadySet) bodyRenderer.sprite = bodySet.idleBody;
             // Reset or clear animation sprites in AgentMover if setup failed
             agentMover?.SetAnimationSprites(null, null, null);
        }

        // Set the initial face emotion to Neutral
        SetEmotion(Emotion.Neutral);
    }
	
	public Sprite GetPortraitSprite()
    {
        // Если портрет был назначен — возвращаем его
        if (assignedPortrait != null) return assignedPortrait;
        
        // Если нет — возвращаем хотя бы тело (чтобы не было пустоты)
        return bodyRenderer != null ? bodyRenderer.sprite : null;
    }

    /// <summary>
    /// Returns the body renderer for external access.
    /// </summary>
    public SpriteRenderer GetBodyRenderer()
    {
        return bodyRenderer;
    }

    /// <summary>
    /// Sets the face sprite based on the provided Emotion enum value.
    /// </summary>
    public void SetEmotion(Emotion emotion)
    {
        // Check required components
        if (currentSpriteCollection == null || faceRenderer == null) return;
        // Get and set the appropriate face sprite
        faceRenderer.sprite = currentSpriteCollection.GetFaceSprite(emotion, characterGender);
    }

    /// <summary>
    /// Sets the face emotion based on the character's current state Enum.
    /// Uses the StateEmotionMap to find the corresponding emotion.
    /// </summary>
    public void SetEmotionForState(System.Enum state)
    {
        // If no state map is assigned, default to Neutral
        if (currentStateEmotionMap == null)
        {
             SetEmotion(Emotion.Neutral);
             // Optionally log a warning if the map is expected but missing
             // Debug.LogWarning($"StateEmotionMap не назначен для {gameObject.name}. Невозможно установить эмоцию для состояния {state.ToString()}.", gameObject);
             return;
        }

        // Try to get the emotion mapped to the state name
        if (currentStateEmotionMap.TryGetEmotionForState(state.ToString(), out Emotion emotionToShow))
        {
            SetEmotion(emotionToShow); // Set the found emotion
        }
        else // If no mapping found for this state
        {
            SetEmotion(Emotion.Neutral); // Default to Neutral
            // Optionally log a warning that a mapping was missing
            // Debug.LogWarning($"Эмоция для состояния '{state.ToString()}' не найдена в StateEmotionMap '{currentStateEmotionMap.name}'. Установлена Neutral.", gameObject);
        }
    }

	/// <summary>
    /// Equips an accessory prefab to the appropriate attachment point (Head or Hand).
    /// Removes any previously equipped accessories.
    /// </summary>
    public void EquipAccessory(GameObject newAccessoryPrefab)
    {
        // Check if attachment points exist
        if (headAttachPoint == null && handAttachPoint == null)
        {
             Debug.LogWarning($"Нет точек крепления (Head/Hand) для аксессуаров у {gameObject.name}!", gameObject);
            return;
        }

        // --- Remove Old Accessories ---
        if (headAttachPoint != null) {
            // Destroy children immediately or deferred depending on context
            for (int i = headAttachPoint.childCount - 1; i >= 0; i--) {
                 Destroy(headAttachPoint.GetChild(i).gameObject);
            }
        }
        if (handAttachPoint != null) {
            for (int i = handAttachPoint.childCount - 1; i >= 0; i--) {
                 Destroy(handAttachPoint.GetChild(i).gameObject);
            }
        }
        // --- End Remove Old ---


        // If no new accessory prefab is provided, we're done
        if (newAccessoryPrefab == null) return;

        // --- Determine Target Attachment Point ---
        Transform targetAttachPoint = handAttachPoint; // Default to hand
        // Check if accessory name suggests it goes on the head and if head point exists
        if (headAttachPoint != null && (newAccessoryPrefab.name.ToLower().Contains("cap") || newAccessoryPrefab.name.ToLower().Contains("hat")))
        {
            targetAttachPoint = headAttachPoint;
        }
         // Ensure the chosen target point actually exists
         if (targetAttachPoint == null) {
              Debug.LogWarning($"Не найдена подходящая точка крепления ({ (targetAttachPoint == headAttachPoint ? "Head" : "Hand") }) для аксессуара '{newAccessoryPrefab.name}' у {gameObject.name}.");
              return; // Cannot attach if the point is missing
         }
        // --- End Determine Target ---

        // --- Instantiate and Attach New Accessory ---
        GameObject accessoryInstance = Instantiate(newAccessoryPrefab, targetAttachPoint);
        // Reset local position and rotation relative to the attachment point
        accessoryInstance.transform.localPosition = Vector3.zero;
        accessoryInstance.transform.localRotation = Quaternion.identity;
        // Optional: Adjust sorting layer/order if needed
        // SpriteRenderer accessoryRenderer = accessoryInstance.GetComponentInChildren<SpriteRenderer>();
        // if (accessoryRenderer != null && bodyRenderer != null) {
        //     accessoryRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
        //     accessoryRenderer.sortingOrder = bodyRenderer.sortingOrder + 1; // Example: draw over body
        // }
         Debug.Log($"Аксессуар {newAccessoryPrefab.name} прикреплен к {targetAttachPoint.name} у {gameObject.name}.");
        // --- End Instantiate ---
    }


    /// <summary>
    /// Plays the visual level-up effect with optional animation.
    /// </summary>
    /// <param name="duration">Total duration of the effect in seconds.</param>
    public void PlayLevelUpEffect(float duration = 1.5f)
    {
        // --- Play Sound ---
        if (levelUpSound != null)
        {
            // Use PlayClipAtPoint for a simple 3D sound effect at the character's position
            // Ensure there's an AudioListener in the scene (usually on the Camera)
            AudioSource.PlayClipAtPoint(levelUpSound, transform.position, 1.0f); // Adjust volume (1.0f) as needed
        }
        // --- End Play Sound ---

        // Check if the effect renderer is assigned
        if (levelUpEffectRenderer == null)
        {
            // Log a warning only if sound was also missing, maybe it's intentional?
             if (levelUpSound == null)
                Debug.LogWarning($"LevelUpEffectRenderer не назначен, визуальный эффект повышения не может быть показан у {gameObject.name}.", gameObject);
            return; // Exit if no visual effect can be played
        }

        // Stop the previous effect coroutine if it's still running
        if (levelUpCoroutine != null)
        {
            StopCoroutine(levelUpCoroutine);
             // Ensure renderer is disabled if interrupted
             if (levelUpEffectRenderer != null) levelUpEffectRenderer.enabled = false;
        }
        // Start the new effect coroutine
        levelUpCoroutine = StartCoroutine(LevelUpEffectRoutine(duration));
    }


    /// <summary>
    /// Coroutine managing the display and fade animation of the level-up effect sprite.
    /// </summary>
    private IEnumerator LevelUpEffectRoutine(float duration)
    {
        if (levelUpEffectRenderer == null) yield break;
        if (duration <= 0) duration = 1.5f;

        levelUpEffectRenderer.gameObject.SetActive(true); // Используем SetActive

        float fadeDuration = duration / 2f;
        Color originalColor = levelUpEffectRenderer.color;
        Color fullyVisibleColor = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
        Color fullyTransparentColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);

        float timer = 0f;

        // Fade In
        while (timer < fadeDuration)
        {
            if (levelUpEffectRenderer == null) yield break;
            
            // <<< ИЗМЕНЕНИЕ ЗДЕСЬ >>>
            timer += Time.unscaledDeltaTime; // Используем unscaledDeltaTime
            
            levelUpEffectRenderer.color = Color.Lerp(fullyTransparentColor, fullyVisibleColor, timer / fadeDuration);
            yield return null;
        }
        if (levelUpEffectRenderer != null) levelUpEffectRenderer.color = fullyVisibleColor;

        // Fade Out
        timer = 0f;
        while (timer < fadeDuration)
        {
            if (levelUpEffectRenderer == null) yield break;
            
            // <<< ИЗМЕНЕНИЕ ЗДЕСЬ >>>
            timer += Time.unscaledDeltaTime; // Используем unscaledDeltaTime
            
            levelUpEffectRenderer.color = Color.Lerp(fullyVisibleColor, fullyTransparentColor, timer / fadeDuration);
            yield return null;
        }

        // --- Cleanup ---
        if (levelUpEffectRenderer != null)
        {
            levelUpEffectRenderer.gameObject.SetActive(false); // Используем SetActive
            levelUpEffectRenderer.color = originalColor;
        }
        levelUpCoroutine = null;
    }

    // Helper to find child recursively (optional, can replace Find calls in Awake)
    public static Transform FindDeepChild(Transform parent, string name) // <<<< ДОБАВЛЕНО 'static'
 {
     if (parent == null) return null;
     Transform result = parent.Find(name);
     if (result != null)
         return result;
     foreach (Transform child in parent)
     {
         result = FindDeepChild(child, name); // Recursive call remains the same
         if (result != null)
             return result;
     }
     return null;
 }

    /// <summary>
    /// Настраивает визуальное разнообразие на основе архетипа клиента.
    /// </summary>
    public void SetupFromArchetype(global::Characters.ClientArchetype archetype)
    {
        if (archetype == null) return;

        currentArchetype = archetype;

        // CRITICAL: Set flag AND actual sprite so visuals.Setup() works correctly
        bodySpriteSetByArchetype = true;

        // Set spriteCollection for face emotions
        if (archetype.spriteCollection != null)
        {
            currentSpriteCollection = archetype.spriteCollection;
        }

        // Set the body sprite now - this is what Setup() checks with bodySpriteAlreadySet = bodySpriteSetByArchetype
        // And SetupVisualDiversity() will also set it, but this ensures it works even if SetupVisualDiversity is called later
        if (bodyRenderer != null && archetype.bodySprite != null)
        {
            bodyRenderer.sprite = archetype.bodySprite;
            Debug.Log($"[CharacterVisuals] SetupFromArchetype: body sprite set to {archetype.bodySprite.name}");
        }
        else if (bodyRenderer != null)
        {
            Debug.LogWarning($"[CharacterVisuals] SetupFromArchetype: archetype.bodySprite is null!");
        }

        Debug.Log($"[CharacterVisuals] SetupFromArchetype: {archetype.displayName}, spriteCollection={(currentSpriteCollection != null ? currentSpriteCollection.name : "NULL")}");
    }

    /// <summary>
    /// Возвращает визуальное разнообразие к базовому состоянию.
    /// </summary>
    public void ResetVisualDiversity()
    {
        if (bodyRenderer != null)
        {
            bodyRenderer.color = Color.white;
        }
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }

    } // End of CharacterVisuals class
} // End of Characters namespace