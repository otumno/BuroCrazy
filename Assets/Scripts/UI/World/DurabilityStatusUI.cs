// Assets/Scripts/UI/World/DurabilityStatusUI.cs
using UnityEngine;
using TMPro;
using Gameplay;

namespace UI.World
{
    [RequireComponent(typeof(Canvas))]
    public class DurabilityStatusUI : MonoBehaviour
    {
        [Header("Ссылки")]
        public OfficeObjectDurability target;
        public TextMeshProUGUI textComponent;

        [Header("Настройки")]
        public float fontSize = 26f;

        private Canvas _canvas;
        private Camera _mainCamera;

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _mainCamera = Camera.main;

            // Настраиваем Canvas программно для World Space
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = _mainCamera;
            
            // ВАЖНО: Слой "UI" обычно рисуется поверх пост-эффектов (включая ч/б фильтр паузы)
            _canvas.sortingLayerName = "UI"; 
            _canvas.sortingOrder = 100; // Поверх большинства объектов

            // Авто-поиск компонентов, если не назначены
            if (target == null) target = GetComponentInParent<OfficeObjectDurability>();
            if (textComponent == null) textComponent = GetComponentInChildren<TextMeshProUGUI>();

            // Скрываем при старте
            _canvas.enabled = false;
        }

        void LateUpdate()
        {
            if (target == null || textComponent == null) return;

            // Проверяем паузу
            bool isPaused = Time.timeScale == 0f;

            // Переключаем видимость
            if (_canvas.enabled != isPaused)
            {
                _canvas.enabled = isPaused;
                if (isPaused) UpdateVisuals(); // Обновляем сразу при включении
            }

            // Если пауза активна — обновляем каждый кадр (на случай ремонта во время паузы или просто для цвета)
            if (isPaused)
            {
                // Поворачиваем текст всегда к камере (чтобы не был зеркальным или боком)
                transform.rotation = Quaternion.identity;
                UpdateVisuals();
            }
        }

        private void UpdateVisuals()
        {
            float ratio = target.currentHealth / target.maxHealth;
            
            // Текст: 85%
            textComponent.text = $"{ratio:P0}";
            textComponent.fontSize = fontSize;

            // Цвет: От Красного (0%) к Белому (100%)
            textComponent.color = Color.Lerp(Color.red, Color.white, ratio);
        }
    }
}