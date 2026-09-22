// Assets/Scripts/UI/Map/CurrentJobTitleUI.cs
// Верхняя панель: отображает полное название текущей должности директора.
// Подписывается на OnProgressionUpdated и автоматически обновляется.

using TMPro;
using UnityEngine;
using Managers;
using Scriptables.Progression;
using System.Collections.Generic;
using System.Linq;

namespace UI.Map
{
    public class CurrentJobTitleUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Текст")]
        [Tooltip("Префикс перед списком должностей.")]
        [SerializeField] private string prefix = "Должность: ";
        [Tooltip("Разделитель между несколькими должностями.")]
        [SerializeField] private string separator = ", ";

        private void OnEnable()
        {
            Refresh();
            if (ProgressionManager.Instance != null)
                ProgressionManager.Instance.OnProgressionUpdated += Refresh;
        }

        private void OnDisable()
        {
            if (ProgressionManager.Instance != null)
                ProgressionManager.Instance.OnProgressionUpdated -= Refresh;
        }

        /// <summary>
        /// Обновить текстовое поле со списком открытых должностей,
        /// отсортированных по tierLevel (от меньшего к большему).
        /// </summary>
        public void Refresh()
        {
            if (titleText == null) return;
            if (ProgressionManager.Instance == null)
            {
                titleText.text = prefix + "—";
                return;
            }

            var allJobs = ProgressionManager.Instance.allJobsDatabase;
            if (allJobs == null || allJobs.Count == 0)
            {
                titleText.text = prefix + "—";
                return;
            }

            // Собираем открытые должности, сортируем по tierLevel (по возрастанию)
            var unlockedTitles = allJobs
                .Where(j => j != null && ProgressionManager.Instance.IsJobUnlocked(j.jobID))
                .OrderBy(j => j.tierLevel)
                .Select(j => j.titleName)
                .ToList();

            string joined = string.Join(separator, unlockedTitles);
            titleText.text = prefix + (string.IsNullOrEmpty(joined) ? "—" : joined);
        }
    }
}
