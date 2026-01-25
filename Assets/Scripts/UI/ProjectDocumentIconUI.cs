// Assets/Scripts/UI/ProjectDocumentIconUI.cs
using UnityEngine;
using UnityEngine.UI;
using Data.Documents;
using TMPro;
using Enums;

namespace UI
{
    public class ProjectDocumentIconUI : MonoBehaviour
    {
        [Header("UI Ссылки")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button button;

        private ProjectDocumentDefinition docData;
        private System.Action<ProjectDocumentDefinition> onClickCallback;

        public void Setup(ProjectDocumentDefinition data, System.Action<ProjectDocumentDefinition> onClick)
        {
            docData = data;
            onClickCallback = onClick;

            if (titleText != null) titleText.text = data.documentName;
            
            // Здесь можно менять цвет иконки в зависимости от типа (Регион - красный, Апгрейд - синий)
            if (iconImage != null)
            {
                if (data.docType == ProjectDocumentType.RegionUnlock) iconImage.color = new Color(1f, 0.5f, 0.5f); // Красноватый
                else if (data.docType == ProjectDocumentType.FacilityUpgrade) iconImage.color = new Color(0.5f, 0.5f, 1f); // Синеватый
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClickCallback?.Invoke(docData));
        }
    }
}