// Assets/Scripts/UI/Map/RegionSlotUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scriptables.Progression;
using Managers;

namespace UI.Map
{
    public class RegionSlotUI : MonoBehaviour
    {
        [Header("Данные")]
        public RegionData regionData;

        [Header("UI Компоненты")]
        [SerializeField] private Image regionImage; // Картинка региона
        [SerializeField] private Image lockIcon;    // Иконка замка
        [SerializeField] private Button selectButton;
        
        [Header("Цвета")]
        [SerializeField] private Color lockedColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color unlockedColor = Color.white;

        private MapPanelUI mapController;

        public void Setup(RegionData data, MapPanelUI controller)
        {
            regionData = data;
            mapController = controller;

            if (regionData != null && regionImage != null)
            {
                regionImage.sprite = regionData.mapVisual;
            }

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnClicked);

            UpdateState();
        }

        public void UpdateState()
        {
            if (regionData == null || ProgressionManager.Instance == null) return;

            bool isUnlocked = ProgressionManager.Instance.IsRegionUnlocked(regionData.regionID);

            if (regionImage != null)
                regionImage.color = isUnlocked ? unlockedColor : lockedColor;

            if (lockIcon != null)
                lockIcon.gameObject.SetActive(!isUnlocked);
        }

        private void OnClicked()
        {
            // Сообщаем контроллеру, что выбрали этот регион (чтобы показать инфо-панель)
            mapController.ShowRegionInfo(regionData);
        }
    }
}