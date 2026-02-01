// Assets/Scripts/UI/Map/JobNodeUI.cs
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

        [Header("Цвета")]
        [SerializeField] private Color ownedColor = Color.green;
        [SerializeField] private Color availableColor = Color.yellow;
        [SerializeField] private Color lockedColor = Color.gray;

        private MapPanelUI mapController;

        public void Setup(JobTitleData data, MapPanelUI controller)
        {
            jobData = data;
            mapController = controller;

            if (titleText != null) titleText.text = data.titleName;

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnClicked);
            }

            UpdateState();
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

            if (lockedOverlay != null) lockedOverlay.SetActive(!isOwned && !isAvailable);

            if (bgImage != null)
            {
                if (isOwned) bgImage.color = ownedColor;
                else if (isAvailable) bgImage.color = availableColor;
                else bgImage.color = lockedColor;
            }
        }

        private void OnClicked()
        {
            Debug.Log($"[JobNodeUI] OnClicked called for job: {jobData?.jobID ?? "NULL"}");

            if (mapController == null)
            {
                Debug.LogError("[JobNodeUI] mapController is null! Make sure MapPanelUI is assigned in Inspector.");
                return;
            }

            Debug.Log($"[JobNodeUI] Calling mapController.ShowJobInfo for {jobData?.jobID ?? "NULL"}");
            mapController.ShowJobInfo(jobData);
        }
    }
}