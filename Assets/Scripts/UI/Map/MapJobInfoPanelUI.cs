// Assets/Scripts/UI/Map/MapJobInfoPanelUI.cs
// Инфо-панель выбранной должности на дереве карьеры.
// Управляет отображением информации о JobTitleData: title, case title,
// description, условия правильного и обходного путей.

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scriptables.Progression;
using Data.Documents;
using Managers;
using System.Collections.Generic;

namespace UI.Map
{
    /// <summary>
    /// Инфо-панель выбранной должности. Компонент кладётся на MapJobPanel
    /// в дочерние TMP-объекты и кнопки привязываются в инспекторе
    /// (или автоматически через Editor-скрипт UpgradeMapJobPanel).
    /// </summary>
    public class MapJobInfoPanelUI : MonoBehaviour
    {
        [Header("Тексты")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI caseTitleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI correctPathText;
        [SerializeField] private TextMeshProUGUI alternatePathText;

        [Header("Кнопки путей")]
        [SerializeField] private Button correctPathButton;
        [SerializeField] private TextMeshProUGUI correctPathButtonLabel;
        [SerializeField] private Button alternatePathButton;
        [SerializeField] private TextMeshProUGUI alternatePathButtonLabel;

        [Header("Кнопка закрытия (опционально)")]
        [SerializeField] private Button closeButton;

        private JobTitleData currentJob;

        private void Awake()
        {
            if (correctPathButton != null)
                correctPathButton.onClick.AddListener(OnCorrectPathClicked);
            if (alternatePathButton != null)
                alternatePathButton.onClick.AddListener(OnAlternatePathClicked);
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            Hide();
        }

        /// <summary>
        /// Отобразить информацию о выбранной должности.
        /// </summary>
        public void ShowJobInfo(JobTitleData job)
        {
            if (job == null) { Hide(); return; }

            currentJob = job;

            if (titleText != null) titleText.text = job.titleName ?? "—";
            if (caseTitleText != null) caseTitleText.text = string.IsNullOrEmpty(job.caseTitle) ? "" : job.caseTitle;
            if (descriptionText != null) descriptionText.text = job.description ?? "";

            if (correctPathText != null)
                correctPathText.text = FormatPath("Правильный путь", job.correctPath, isAlternate: false);

            if (alternatePathText != null)
                alternatePathText.text = FormatPath("Обходной путь", job.alternatePath, isAlternate: true);

            // Кнопки
            if (correctPathButton != null)
            {
                bool valid = job.correctPath != null && job.correctPath.isValid;
                correctPathButton.interactable = valid;
                if (correctPathButtonLabel != null)
                    correctPathButtonLabel.text = valid ? "Правильный путь" : "Недоступно";
            }

            if (alternatePathButton != null)
            {
                bool valid = job.alternatePath != null && job.alternatePath.isValid;
                alternatePathButton.interactable = valid;
                if (alternatePathButtonLabel != null)
                    alternatePathButtonLabel.text = valid ? "Обходной путь" : "Недоступно";
            }

            gameObject.SetActive(true);
        }

        /// <summary>
        /// Скрыть инфо-панель.
        /// </summary>
        public void Hide()
        {
            currentJob = null;
            gameObject.SetActive(false);
        }

        // Форматирование строки условий пути для TMP rich text.
        private static string FormatPath(string header, JobTitleData.PathData path, bool isAlternate)
        {
            if (path == null || !path.isValid)
                return $"<b>{header}:</b> <color=#888888>недоступен</color>";

            var parts = new List<string>();

            if (path.requiredDays > 0) parts.Add($"{path.requiredDays} дн.");
            if (path.requiredClients > 0) parts.Add($"{path.requiredClients} клиентов");
            if (path.requiredStaff > 0) parts.Add($"{path.requiredStaff} сотрудников");
            if (path.requiredRegions > 0) parts.Add($"{path.requiredRegions} регионов");
            if (path.requiredUpgrades > 0) parts.Add($"{path.requiredUpgrades} апгрейдов");
            if (path.requiredDocuments > 0) parts.Add($"{path.requiredDocuments} документов");
            if (path.requiredMoney > 0) parts.Add($"{path.requiredMoney}$");

            string conditions = parts.Count > 0 ? string.Join(", ", parts) : "без условий";

            string cost = "";
            if (isAlternate && (path.moneyCost > 0 || path.corruptionCost > 0))
            {
                var costParts = new List<string>();
                if (path.moneyCost > 0) costParts.Add($"{path.moneyCost}$");
                if (path.corruptionCost > 0) costParts.Add($"+{path.corruptionCost}% коррупции");
                cost = $"  |  Цена: {string.Join(", ", costParts)}";
            }

            return $"<b>{header}:</b> {conditions}{cost}";
        }

        // Запускают процесс повышения через ProgressionManager.
        private void OnCorrectPathClicked()
        {
            if (currentJob == null) return;
            if (ProgressionManager.Instance == null)
            {
                Debug.LogError("[MapJobInfoPanelUI] ProgressionManager.Instance не найден.");
                return;
            }

            ProgressionManager.Instance.StartJobPromotion(currentJob,
                ProjectDocumentDefinition.PathType.Correct);
            Hide();
        }

        private void OnAlternatePathClicked()
        {
            if (currentJob == null) return;
            if (ProgressionManager.Instance == null)
            {
                Debug.LogError("[MapJobInfoPanelUI] ProgressionManager.Instance не найден.");
                return;
            }

            ProgressionManager.Instance.StartJobPromotion(currentJob,
                ProjectDocumentDefinition.PathType.Alternate);
            Hide();
        }
    }
}
