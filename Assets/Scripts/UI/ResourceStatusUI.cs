// Assets/Scripts/UI/ResourceStatusUI.cs
using UnityEngine;
using TMPro;
using Gameplay; // Подключаем, чтобы видеть ResourceContainer

namespace UI
{
    [RequireComponent(typeof(Canvas))]
    public class ResourceStatusUI : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Перетащите сюда ResourceContainer с родительского объекта")]
        public ResourceContainer targetContainer;
        [Tooltip("Текстовое поле для процентов")]
        public TextMeshProUGUI percentText;

        [Header("Настройки")]
        public Color normalColor = Color.white;
        public Color criticalColor = Color.red;

        private Canvas _canvas;
        private Camera _mainCamera;

        void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _mainCamera = Camera.main;
            
            // Настраиваем Canvas программно, чтобы не мучиться в инспекторе
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = _mainCamera;
            // Ставим высокий порядок сортировки, чтобы текст был поверх спрайтов
            _canvas.sortingLayerName = "UI"; 
            _canvas.sortingOrder = 100; 

            if (targetContainer == null)
            {
                targetContainer = GetComponentInParent<ResourceContainer>();
            }
            
            // Скрываем при старте
            _canvas.enabled = false;
        }

        void LateUpdate()
        {
            if (targetContainer == null || percentText == null) return;

            // 1. Проверяем Паузу (Time.timeScale == 0)
            bool isPaused = Time.timeScale == 0f;

            // Включаем/Выключаем Canvas
            if (_canvas.enabled != isPaused)
            {
                _canvas.enabled = isPaused;
                // Если только что включили - обновим текст сразу
                if (isPaused) UpdateText();
            }

            // Если пауза активна - обновляем текст (на случай изменений)
            if (isPaused)
            {
                // Поворачиваем текст к камере (чтобы не было зеркально/криво)
                transform.rotation = Quaternion.identity;
                UpdateText();
            }
        }

        private void UpdateText()
        {
            float percent = (float)targetContainer.currentAmount / targetContainer.maxAmount;
            percentText.text = $"{percent:P0}"; // Формат процентов (например, "75%")

            // Меняем цвет, если ресурса мало
            if (targetContainer.currentAmount <= targetContainer.criticalThreshold)
            {
                percentText.color = criticalColor;
            }
            else
            {
                percentText.color = normalColor;
            }
        }
    }
}