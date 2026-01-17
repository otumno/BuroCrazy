// Assets/Scripts/Gameplay/OfficeObjectDurability.cs
using UnityEngine;
using UnityEngine.Events;

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

        [Header("Визуал")]
        [Tooltip("Если пусто, скрипт попытается найти SpriteRenderer на этом объекте или внутри него")]
        public SpriteRenderer targetRenderer;
        public Sprite pristineSprite;
        public Sprite wornSprite;
        public Sprite brokenSprite;
        
        [Header("Эффекты")]
        public GameObject brokenEffect;
        public AudioClip breakSound;

        [Header("События")]
        public UnityEvent OnBreak;
        public UnityEvent OnRepair;

        public State CurrentState { get; private set; } = State.Pristine;

        private void Awake()
        {
            // АВТО-ПОИСК: Если рендерер не назначен вручную
            if (targetRenderer == null) 
            {
                // Сначала ищем на себе
                targetRenderer = GetComponent<SpriteRenderer>();
                // Если нет, ищем в дочерних объектах (для раздельной логики/визуала)
                if (targetRenderer == null)
                    targetRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            currentHealth = maxHealth;
            UpdateVisuals();
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
            OnRepair?.Invoke();
        }

        private void CheckState()
        {
            float ratio = currentHealth / maxHealth;
            State oldState = CurrentState;

            if (ratio <= brokenThreshold) CurrentState = State.Broken;
            else if (ratio <= wornThreshold) CurrentState = State.Worn;
            else CurrentState = State.Pristine;

            if (oldState != CurrentState)
            {
                UpdateVisuals();
                if (CurrentState == State.Broken && oldState != State.Broken)
                {
                    if (breakSound != null) AudioSource.PlayClipAtPoint(breakSound, transform.position);
                    OnBreak?.Invoke();
                    Debug.Log($"<color=red>[Durability]</color> {name} СЛОМАЛСЯ!");
                }
            }
        }

        private void UpdateVisuals()
        {
            // Если рендерера нет вообще, просто игнорируем визуал, но логика работает
            if (targetRenderer == null) return;

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

        public float GetEfficiencyMultiplier()
        {
            switch (CurrentState)
            {
                case State.Pristine: return 1.0f;
                case State.Worn: return 0.7f; // На 30% медленнее
                case State.Broken: return 0.0f; // Не работает
                default: return 1.0f;
            }
        }
        
        public bool IsUsable()
        {
            return CurrentState != State.Broken;
        }
    }
}