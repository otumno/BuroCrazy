// Assets/Scripts/UI/Map/JobNodeUI.cs

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scriptables.Progression;
using Managers;

namespace UI.Map
{
    public class JobNodeUI : MonoBehaviour
    {
        [Header("Данные")]
        public JobTitleData jobData;

        [Header("UI Компоненты")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Image bgImage;
        [SerializeField] private Button selectButton;
        [SerializeField] private GameObject lockedOverlay;

        [Header("Иконка должности")]
        [Tooltip("Иконка должности (ЧБ или цветная). Если null — используется fallback на bgImage.color.")]
        [SerializeField] private Image iconImage;
        [Tooltip("Рамка вокруг иконки (опционально).")]
        [SerializeField] private Image frameImage;

        [Header("Цвета (fallback если иконок нет)")]
        [SerializeField] private Color ownedColor = Color.green;
        [SerializeField] private Color availableColor = Color.yellow;
        [SerializeField] private Color lockedColor = Color.gray;

        public void Setup(JobTitleData data, Action<JobTitleData> onClick)
        {
            jobData = data;

            if (titleText != null) titleText.text = data.titleName;

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => onClick?.Invoke(jobData));
            }

            UpdateState();
        }

        /// <summary>
        /// Перезагрузить отображение без повторного Setup.
        /// Нужен для внешнего обновления (например, после изменения состояния прогрессии).
        /// </summary>
        public void RefreshFromData()
        {
            UpdateState();
        }

        private void OnEnable()
        {
            // Подписываемся на обновления прогрессии — нода автоматически перерисуется
            // при открытии новой должности, региона или смены других условий.
            if (ProgressionManager.Instance != null)
            {
                ProgressionManager.Instance.OnProgressionUpdated += RefreshFromData;
            }
        }

        private void OnDisable()
        {
            if (ProgressionManager.Instance != null)
            {
                ProgressionManager.Instance.OnProgressionUpdated -= RefreshFromData;
            }
        }

        public void UpdateState()
        {
            if (jobData == null)
            {
                Debug.LogWarning("[JobNodeUI] jobData is null!");
                return;
            }

            if (ProgressionManager.Instance == null)
            {
                Debug.LogWarning("[JobNodeUI] ProgressionManager.Instance is null!");
                return;
            }

            bool isOwned = ProgressionManager.Instance.IsJobUnlocked(jobData.jobID);
            bool isAvailable = !isOwned && ProgressionManager.Instance.CanStartUnlockJob(jobData);
            bool isLocked = !isOwned && !isAvailable;

            // lockedOverlay показываем только если должность заблокирована
            if (lockedOverlay != null) lockedOverlay.SetActive(isLocked);

            // Если в JobTitleData есть иконки — используем Вариант А (иконка + цвет рамки).
            // Иначе — fallback на bgImage.color (Вариант Б).
            bool hasIcons = (jobData.iconLocked != null) || (jobData.iconUnlocked != null);

            if (hasIcons)
            {
                ApplyIconVariant(isOwned, isAvailable, isLocked);
            }
            else
            {
                ApplyColorFallback(isOwned, isAvailable, isLocked);
            }
        }

        // ----- Вариант А: иконки заданы в JobTitleData -----
        private void ApplyIconVariant(bool isOwned, bool isAvailable, bool isLocked)
        {
            if (iconImage != null)
            {
                iconImage.enabled = true;

                if (isOwned)
                {
                    iconImage.sprite = jobData.iconUnlocked != null ? jobData.iconUnlocked : jobData.iconLocked;
                    iconImage.color = Color.white;
                }
                else
                {
                    // isAvailable или isLocked — показываем ЧБ-иконку
                    iconImage.sprite = jobData.iconLocked;
                    iconImage.color = Color.white;
                }
            }

            // Цвет рамки: зелёный/жёлтый/серый
            Color frameColor;
            if (isOwned) frameColor = ownedColor;
            else if (isAvailable) frameColor = availableColor;
            else frameColor = lockedColor;

            if (frameImage != null)
            {
                frameImage.color = frameColor;
            }
            else if (bgImage != null)
            {
                // fallback: если рамки нет, красим bgImage
                bgImage.color = frameColor;
            }
        }

        // ----- Вариант Б: иконок нет, красим фон (старая логика) -----
        private void ApplyColorFallback(bool isOwned, bool isAvailable, bool isLocked)
        {
            if (iconImage != null) iconImage.enabled = false;

            if (bgImage == null) return;

            if (isOwned) bgImage.color = ownedColor;
            else if (isAvailable) bgImage.color = availableColor;
            else bgImage.color = lockedColor;
        }
    }
}
