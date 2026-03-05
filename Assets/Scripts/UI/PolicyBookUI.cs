using UnityEngine;
using UnityEngine.UI;
using Data.Policies;
using Managers;
using Managers.Teletype;
using TMPro;

namespace UI
{
    public class PolicyBookUI : MonoBehaviour
    {
        [Header("UI Элементы")]
        public Transform listContainer;
        public GameObject policyItemPrefab;
        public Button closeButton;

        [Header("Детали")]
        public GameObject detailsPanel;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI descText;
        public TextMeshProUGUI statsText;
        public Button draftButton;

        private PolicyData selectedPolicy;

        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (draftButton != null) draftButton.onClick.AddListener(OnDraftClicked);
            detailsPanel.SetActive(false);
        }

        public void Show()
        {
            var animator = GetComponent<UIWindowAnimator>();
            if (animator != null) animator.Open();
            else gameObject.SetActive(true);

            MainUIManager.Instance?.PushPause();
            RefreshList();
            detailsPanel.SetActive(false);
        }

        public void Hide()
        {
            var animator = GetComponent<UIWindowAnimator>();
            if (animator != null) animator.Close();
            else
            {
                gameObject.SetActive(false);
                MainUIManager.Instance?.PopPause();
            }
        }

        private void RefreshList()
        {
            if (PolicyManager.Instance == null) return;

            foreach (Transform child in listContainer) Destroy(child.gameObject);

            var policies = PolicyManager.Instance.allPoliciesDatabase;
            foreach (var policy in policies)
            {
                if (policy == null) continue;

                bool isActive = PolicyManager.Instance.activePolicyIDs.Contains(policy.id);
                bool isPending = DocumentManager.Instance != null && DocumentManager.Instance.IsProjectDocPending(policy.id);

                GameObject item = Instantiate(policyItemPrefab, listContainer);
                var btn = item.GetComponent<Button>();
                var txt = item.GetComponentInChildren<TextMeshProUGUI>();
                
                string status = isActive ? " <color=green>[АКТИВНО]</color>" : (isPending ? " <color=yellow>[В ПУТИ]</color>" : "");
                txt.text = policy.displayName + status;

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

            string stats = "";
            if (policy.workSpeedMultiplier != 1f) stats += $"Эффективность: {policy.workSpeedMultiplier:P0}\n";
            if (policy.stressGrowthMultiplier != 1f) stats += $"Стресс: {policy.stressGrowthMultiplier:P0}\n";
            if (policy.incomeMultiplier != 1f) stats += $"Доход: {policy.incomeMultiplier:P0}\n";
            
            statsText.text = string.IsNullOrEmpty(stats) ? "Влияет на поведение персонала." : stats;
        }

        private void OnDraftClicked()
        {
            if (selectedPolicy != null && PolicyManager.Instance != null)
            {
                PolicyManager.Instance.DraftPolicy(selectedPolicy.id);
                TeletypeManager.Instance?.Log($"Проект указа '{selectedPolicy.displayName}' создан.");
                Hide();
            }
        }
    }
}
