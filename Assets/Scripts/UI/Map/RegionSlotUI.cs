// Assets/Scripts/UI/Map/RegionSlotUI.cs

using System;
using UnityEngine;
using UnityEngine.UI;
using Scriptables.Progression;
using Managers;

namespace UI.Map
{
    public class RegionSlotUI : MonoBehaviour
    {
        [Header("Данные")]
        public RegionData regionData;

        [Header("UI Компоненты")]
        [SerializeField] private Image regionImage;
        [SerializeField] private Image lockIcon;
        [SerializeField] private Button selectButton;
        
        [Header("Цвета")]
        [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color unlockedColor = Color.white;

        public void Setup(RegionData data, Action<RegionData> onClick)
        {
            regionData = data;

            if (regionData != null && regionImage != null)
            {
                regionImage.sprite = regionData.mapVisual;
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => onClick?.Invoke(regionData));
            }

            UpdateState();
        }

        public void UpdateState()
        {
            if (regionData == null)
            {
                Debug.LogWarning("[RegionSlotUI] regionData is null!");
                return;
            }

            if (ProgressionManager.Instance == null)
            {
                Debug.LogWarning("[RegionSlotUI] ProgressionManager.Instance is null!");
                return;
            }

            bool isUnlocked = ProgressionManager.Instance.IsRegionUnlocked(regionData.regionID);
            
            // --- НОВАЯ ПРОВЕРКА ---
            bool isPending = DocumentManager.Instance != null && DocumentManager.Instance.IsProjectDocPending(regionData.regionID);
            // ----------------------

            if (isUnlocked)
            {
                regionImage.color = unlockedColor;
            }
            else if (isPending)
            {
                regionImage.color = Color.yellow; // Подсветка "В процессе"
            }
            else
            {
                regionImage.color = lockedColor;
            }

            // Скрываем замок, если открыто ИЛИ если в процессе (чтобы было видно желтый цвет)
            lockIcon.gameObject.SetActive(!isUnlocked && !isPending);
            
            // Блокируем клик, если уже в процессе? 
            // Можно оставить кликабельным, чтобы посмотреть инфо, но кнопку "Заказать" заблокируем в MapPanelUI.
        }
    }
}