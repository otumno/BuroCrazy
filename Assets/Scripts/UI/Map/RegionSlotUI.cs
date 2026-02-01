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

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnClicked);
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

            if (regionImage != null)
            {
                if (isUnlocked) regionImage.color = unlockedColor;
                else if (isPending) regionImage.color = Color.yellow; // Подсветка "В процессе"
                else regionImage.color = lockedColor;
            }

            if (lockIcon != null)
            {
                // Скрываем замок, если открыто ИЛИ если в процессе (чтобы было видно желтый цвет)
                lockIcon.gameObject.SetActive(!isUnlocked && !isPending);
            }
            
            // Блокируем клик, если уже в процессе? 
            // Можно оставить кликабельным, чтобы посмотреть инфо, но кнопку "Заказать" заблокируем в MapPanelUI.
        }

        private void OnClicked()
        {
            Debug.Log($"[RegionSlotUI] OnClicked called for region: {regionData?.regionID ?? "NULL"}");

            if (mapController == null)
            {
                Debug.LogError("[RegionSlotUI] mapController is null! Make sure MapPanelUI is assigned in Inspector.");
                return;
            }

            Debug.Log($"[RegionSlotUI] Calling mapController.ShowRegionInfo for {regionData?.regionID ?? "NULL"}");
            // Сообщаем контроллеру, что выбрали этот регион (чтобы показать инфо-панель)
            mapController.ShowRegionInfo(regionData);
        }
    }
}