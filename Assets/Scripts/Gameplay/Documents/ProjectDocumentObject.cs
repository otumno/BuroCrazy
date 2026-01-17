// Assets/Scripts/Gameplay/Documents/ProjectDocumentObject.cs
using UnityEngine;
using TMPro;
using Data.Documents;

namespace Gameplay.Documents
{
    public class ProjectDocumentObject : MonoBehaviour
    {
        [Header("Данные")]
        public ProjectDocumentDefinition documentData;

        [Header("Визуал")]
        public TextMeshPro documentTitleText;
        public GameObject stampDirector;
        public GameObject stampRegistrar;
        public GameObject stampPaid;

        public void Initialize(ProjectDocumentDefinition data)
        {
            this.documentData = data;
            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            if (documentData == null) return;

            if (documentTitleText != null)
                documentTitleText.text = documentData.documentName;

            if (stampDirector != null) stampDirector.SetActive(documentData.signedByDirector);
            if (stampRegistrar != null) stampRegistrar.SetActive(documentData.processedByRegistrar);
            if (stampPaid != null) stampPaid.SetActive(documentData.paidAtCashier);

            // Цветовая кодировка (опционально)
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                // ИСПРАВЛЕНИЕ: Используем глобальный Enum
                if (documentData.docType == ProjectDocumentType.Policy) sr.color = new Color(1f, 0.8f, 0.8f); // Розоватый для законов
                else if (documentData.docType == ProjectDocumentType.RegionUnlock) sr.color = Color.white;
            }
        }
    }
}