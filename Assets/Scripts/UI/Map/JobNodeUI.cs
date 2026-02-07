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

        [Header("Цвета")]
        [SerializeField] private Color ownedColor = Color.green;
        [SerializeField] private Color availableColor = Color.yellow;
        [SerializeField] private Color lockedColor = Color.gray;

        public void Setup(JobTitleData data, Action<JobTitleData> onClick)
        {
            jobData = data;

            titleText.text = data.titleName;

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onClick?.Invoke(jobData));

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

            lockedOverlay.SetActive(!isOwned && !isAvailable);

            if (isOwned)
            {
                bgImage.color = ownedColor;
            }
            else if (isAvailable)
            {
                bgImage.color = availableColor;
            }
            else
            {
                bgImage.color = lockedColor;
            }
        }
    }
}