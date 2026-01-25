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

        [Header("Вкладки")]
        public Button policiesTabButton;
        public Button instructionsTabButton;
        public GameObject policiesPanel;
        public GameObject instructionsPanel;

        [Header("Инструкции")]
        public Transform instructionsContainer;
        public GameObject instructionItemPrefab;

        [Header("Детали")]
        public GameObject detailsPanel;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI descText;
        public TextMeshProUGUI statsText;
        public Button draftButton; // Кнопка "Создать приказ"

        private PolicyData selectedPolicy;
        private bool showingPolicies = true;

        private void Start()
        {
            closeButton.onClick.AddListener(Hide);
            draftButton.onClick.AddListener(OnDraftClicked);

            if (policiesTabButton != null)
            {
                policiesTabButton.onClick.AddListener(() => SwitchTab(true));
            }

            if (instructionsTabButton != null)
            {
                instructionsTabButton.onClick.AddListener(() => SwitchTab(false));
            }

            detailsPanel.SetActive(false);
            gameObject.SetActive(false);

            SwitchTab(true);
        }

        private void SwitchTab(bool showPolicies)
        {
            showingPolicies = showPolicies;

            if (policiesPanel != null)
            {
                policiesPanel.SetActive(showPolicies);
            }

            if (instructionsPanel != null)
            {
                instructionsPanel.SetActive(!showPolicies);
            }

            if (policiesTabButton != null)
            {
                policiesTabButton.interactable = !showPolicies;
            }

            if (instructionsTabButton != null)
            {
                instructionsTabButton.interactable = showPolicies;
            }

            if (showPolicies)
            {
                RefreshList();
            }
            else
            {
                RefreshInstructions();
            }
        }

        private void OnEnable()
        {
            MainUIManager.Instance?.PushPause();

            if (showingPolicies)
            {
                RefreshList();
            }
            else
            {
                RefreshInstructions();
            }
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

        private void RefreshInstructions()
        {
            if (InstructionManager.Instance == null || instructionsContainer == null) return;

            foreach (Transform child in instructionsContainer) Destroy(child.gameObject);

            var instructions = InstructionManager.Instance.GetAllEnabledInstructions();

            foreach (var instruction in instructions)
            {
                GameObject item = Instantiate(instructionItemPrefab, instructionsContainer);
                var itemUI = item.GetComponent<UI.Policies.InstructionItemUI>();

                if (itemUI != null)
                {
                    itemUI.Setup(instruction, InstructionManager.Instance);
                }
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