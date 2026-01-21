// Assets/Scripts/UI/PolicyBookUI.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Data.Policies;
using Managers;
using TMPro;

namespace UI
{
    public class PolicyBookUI : MonoBehaviour
    {
        [Header("Данные")]
        // Берем политики напрямую из менеджера или назначаем здесь список
        // Лучше брать из PolicyManager.Instance.allPoliciesDatabase

        [Header("UI Элементы")]
        public Transform listContainer;
        public GameObject policyItemPrefab; // Кнопка с названием политики
        public Button closeButton;

        [Header("Детали")]
        public GameObject detailsPanel;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI descText;
        public TextMeshProUGUI statsText;
        public Button draftButton; // Кнопка "Создать приказ"

        private PolicyData selectedPolicy;

        private void Start()
        {
            closeButton.onClick.AddListener(Hide);
            draftButton.onClick.AddListener(OnDraftClicked);
            detailsPanel.SetActive(false);
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            MainUIManager.Instance?.PushPause();
            RefreshList();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            MainUIManager.Instance?.PopPause();
        }

        private void RefreshList()
        {
            if (PolicyManager.Instance == null) return;

            foreach (Transform child in listContainer) Destroy(child.gameObject);

            var policies = PolicyManager.Instance.allPoliciesDatabase;
            foreach (var policy in policies)
            {
                // Проверяем, не активна ли уже эта политика
                bool isActive = PolicyManager.Instance.activePolicyIDs.Contains(policy.id);
                bool isPending = DocumentManager.Instance.IsProjectDocPending(policy.id);

                GameObject item = Instantiate(policyItemPrefab, listContainer);
                var btn = item.GetComponent<Button>();
                var txt = item.GetComponentInChildren<TextMeshProUGUI>();
                
                string status = isActive ? " [АКТИВНО]" : (isPending ? " [В ПУТИ]" : "");
                txt.text = policy.displayName + status;

                // Блокируем кнопку, если уже активно или в процессе
                if (isActive || isPending) btn.interactable = false;
                else btn.onClick.AddListener(() => ShowDetails(policy));
            }
        }

        private void ShowDetails(PolicyData policy)
        {
            selectedPolicy = policy;
            detailsPanel.SetActive(true);
            
            titleText.text = policy.displayName;
            descText.text = policy.description;
            statsText.text = $"Эффективность: {policy.workSpeedMultiplier:P0}\nСтресс: {policy.stressGrowthMultiplier:P0}\nДоход: {policy.incomeMultiplier:P0}";
        }

        private void OnDraftClicked()
        {
            if (selectedPolicy != null && PolicyManager.Instance != null)
            {
                PolicyManager.Instance.DraftPolicy(selectedPolicy.id);
                
                TeletypeManager.Instance?.Log($"Проект указа '{selectedPolicy.displayName}' создан.");
                
                gameObject.SetActive(false); // Закрываем книгу
            }
        }
    }
}