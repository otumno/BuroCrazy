// Assets/Scripts/Gameplay/OfficeObjectDurability.cs
using UnityEngine;
using UnityEngine.Events;
using Managers;
using UI.World;
using Scriptables.Audio;

namespace Gameplay
{
    public class OfficeObjectDurability : MonoBehaviour
    {
        public enum State { Pristine, Worn, Broken }

        [Header("Настройки Прочности")]
        [Tooltip("Максимальная прочность")]
        public float maxHealth = 100f;
        [Tooltip("Текущая прочность")]
        public float currentHealth;
        
        [Header("Пороги состояний")]
        [Tooltip("Процент прочности (0-1), ниже которого предмет считается Потертым")]
        [Range(0f, 1f)] public float wornThreshold = 0.7f;
        [Tooltip("Процент прочности (0-1), ниже которого предмет считается Сломанным")]
        [Range(0f, 1f)] public float brokenThreshold = 0.0f;

        [Header("Визуал Объекта")]
        [Tooltip("Если пусто, скрипт попытается найти SpriteRenderer на этом объекте или внутри него")]
        public SpriteRenderer targetRenderer;
        public Sprite pristineSprite;
        public Sprite wornSprite;
        public Sprite brokenSprite;
        
        [Header("Визуал Статуса (UI)")]
        [Tooltip("Ссылка на компонент иконки (WorldSpaceStatusIcon). Если пусто, ищет в детях.")]
        public WorldSpaceStatusIcon statusIconController;
        [Tooltip("Спрайт 'Гаечный ключ' для иконки")]
        public Sprite repairIconSprite;

        [Header("Эффекты и Звук")]
        public GameObject brokenEffect; // Искры (без дыма!)
        
        [Tooltip("ID звука при поломке")]
        public SoundID breakSoundID = SoundID.Object_Break;
        
        [Tooltip("ID звука при починке")]
        public SoundID repairSoundID = SoundID.Object_Repair;

        [Header("События (Unity Events)")]
        [Tooltip("События, вызываемые при поломке (для геймдизайнеров)")]
        public UnityEvent OnBreak;
        [Tooltip("События, вызываемые при починке (для геймдизайнеров)")]
        public UnityEvent OnRepair;

        public State CurrentState { get; private set; } = State.Pristine;

        private void Awake()
        {
            if (targetRenderer == null) 
            {
                targetRenderer = GetComponent<SpriteRenderer>();
                if (targetRenderer == null)
                    targetRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (statusIconController == null)
            {
                statusIconController = GetComponentInChildren<WorldSpaceStatusIcon>();
            }

            currentHealth = maxHealth;
        }

        private void Start()
        {
            if (DurabilityManager.Instance != null)
            {
                DurabilityManager.Instance.RegisterObject(this);
            }
            UpdateVisuals();
        }

        private void OnDestroy()
        {
            if (DurabilityManager.Instance != null)
            {
                DurabilityManager.Instance.UnregisterObject(this);
            }
        }

        public void Degrade(float amount)
        {
            if (currentHealth <= 0) return;
            currentHealth -= amount;
            CheckState();
        }

        public void Repair()
        {
            currentHealth = maxHealth;
            CheckState();
            
            // Звук починки
            if (Managers.AudioManager.Instance != null && repairSoundID != SoundID.None)
            {
                Managers.AudioManager.Instance.PlaySound(repairSoundID, transform.position);
            }
            
            OnRepair?.Invoke();
        }

        // --- ВАЖНО: Сделал PUBLIC, чтобы SaveLoadManager мог обновить состояние после загрузки ---
        public void CheckState()
        {
            float ratio = currentHealth / maxHealth;
            State oldState = CurrentState;

            if (ratio <= brokenThreshold) CurrentState = State.Broken;
            else if (ratio <= wornThreshold) CurrentState = State.Worn;
            else CurrentState = State.Pristine;

            if (oldState != CurrentState)
            {
                UpdateVisuals();
                
                // Звук поломки только при переходе в Broken и только если это произошло в игре (не при загрузке)
                // Но SaveLoadManager вызывает CheckState после присвоения здоровья, 
                // поэтому звук может проиграться при загрузке сломанного стола.
                // Если это нежелательно, можно добавить флаг suppressSound.
                // Пока оставим так для простоты.
                if (CurrentState == State.Broken && oldState != State.Broken)
                {
                    if (Managers.AudioManager.Instance != null && breakSoundID != SoundID.None)
                    {
                        Managers.AudioManager.Instance.PlaySound(breakSoundID, transform.position);
                    }
                    
                    OnBreak?.Invoke();
                    Debug.Log($"<color=red>[Durability]</color> {name} СЛОМАЛСЯ!");
                }
            }
            else
            {
                // Даже если состояние не сменилось (например, при загрузке), визуализацию надо обновить
                UpdateVisuals();
            }
        }

        private void UpdateVisuals()
        {
            if (targetRenderer != null)
            {
                switch (CurrentState)
                {
                    case State.Pristine:
                        if (pristineSprite != null) targetRenderer.sprite = pristineSprite;
                        if (brokenEffect != null) brokenEffect.SetActive(false);
                        break;
                    case State.Worn:
                        if (wornSprite != null) targetRenderer.sprite = wornSprite;
                        if (brokenEffect != null) brokenEffect.SetActive(false);
                        break;
                    case State.Broken:
                        if (brokenSprite != null) targetRenderer.sprite = brokenSprite;
                        if (brokenEffect != null) brokenEffect.SetActive(true);
                        break;
                }
            }

            if (statusIconController != null)
            {
                bool showIcon = (CurrentState == State.Broken); 
                statusIconController.SetIconState(showIcon, repairIconSprite, Color.white); 
            }
        }

        public float GetEfficiencyMultiplier()
        {
            switch (CurrentState)
            {
                case State.Pristine: return 1.0f;
                case State.Worn: return 0.7f;
                case State.Broken: return 0.0f;
                default: return 1.0f;
            }
        }
        
        public bool IsUsable()
        {
            return CurrentState != State.Broken;
        }
    }
}