// Assets/Scripts/UI/ProjectReviewPanelUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Data.Documents;
using Managers;

namespace UI
{
    public class ProjectReviewPanelUI : MonoBehaviour
    {
        [Header("UI Ссылки")]
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Button signButton;
        [SerializeField] private Button closeButton;

        private ProjectDocumentDefinition currentDoc;
        private System.Action onSignedCallback;

        private void Awake()
        {
            closeButton.onClick.AddListener(Hide);
            signButton.onClick.AddListener(OnSignClicked);
            gameObject.SetActive(false);
        }

        public void Show(ProjectDocumentDefinition doc, System.Action onSigned)
        {
            currentDoc = doc;
            onSignedCallback = onSigned;

            // Заполняем данными
            if (doc.docType == ProjectDocumentType.RegionUnlock && doc.targetRegion != null)
            {
                headerText.text = "ПРИКАЗ О РАСШИРЕНИИ";
                descriptionText.text = $"Касательно: Захват района '{doc.targetRegion.displayName}'.\n\n{doc.targetRegion.description}\n\nТребуется подпись для начала процедуры оформления.";
                costText.text = $"Стоимость: {doc.targetRegion.unlockCostMoney}$ / {doc.targetRegion.unlockCostInfluence} Влияния";
            }
            else if (doc.docType == ProjectDocumentType.JobPromotion && doc.targetJob != null)
            {
                headerText.text = "КАДРОВАЯ ПЕРЕСТАНОВКА";
                descriptionText.text = $"Касательно: Назначение на должность '{doc.targetJob.titleName}'.\n\nТребуется утверждение.";
                costText.text = $"Стоимость: {doc.targetJob.costMoney}$ / {doc.targetJob.costInfluence} Влияния";
            }

            // Проверка ресурсов (Визуальная). Реальное списание будет в Кассе, но директор должен знать.
            // Можно сделать кнопку серой, если ресурсов совсем нет, но по логике "Волокиты" 
            // директор может подписать, а кассир потом откажет. Оставим кнопку активной.
            signButton.interactable = true; 

            gameObject.SetActive(true);
        }

        private void OnSignClicked()
        {
            if (currentDoc != null)
            {
                // 1. Ставим подпись в данных
                currentDoc.signedByDirector = true;
                
                // 2. Вызываем коллбек (чтобы обновить физический мир)
                onSignedCallback?.Invoke();
                
                // 3. Звук/Эффект (опционально)
                Debug.Log($"[ProjectReview] Документ '{currentDoc.documentName}' подписан Директором.");
            }
            Hide();
        }

        private void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}