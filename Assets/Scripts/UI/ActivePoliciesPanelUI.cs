// Assets/Scripts/UI/ActivePoliciesPanelUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Managers;
using Data.Policies;
using System.Linq;
using Managers.Teletype;

namespace UI
{
    public class ActivePoliciesPanelUI : MonoBehaviour
    {
        [Header("UI Элементы")]
        public Transform listContainer;
        public GameObject activePolicyItemPrefab; // Кнопка с текстом и кнопкой "Сорвать"
        public Button closeButton;

        private Gameplay.NoticeBoard linkedBoard;

        private void Start()
        {
            closeButton.onClick.AddListener(() => {
                gameObject.SetActive(false);
                MainUIManager.Instance.PopPause();
            });
            gameObject.SetActive(false);
            
            // Находим доску, если не привязана
            if (linkedBoard == null) linkedBoard = FindFirstObjectByType<Gameplay.NoticeBoard>();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            MainUIManager.Instance.PushPause();
            RefreshList();
        }

        private void RefreshList()
        {
            foreach (Transform child in listContainer) Destroy(child.gameObject);

            if (PolicyManager.Instance == null) return;

            foreach (var id in PolicyManager.Instance.activePolicyIDs)
            {
                var policy = PolicyManager.Instance.GetPolicyById(id);
                if (policy != null)
                {
                    CreateItem(policy);
                }
            }
        }

        private void CreateItem(PolicyData policy)
        {
            GameObject item = Instantiate(activePolicyItemPrefab, listContainer);
            // Предполагаем, что в префабе есть текст и кнопка "X"
            var texts = item.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length > 0) texts[0].text = policy.displayName; // Название
            if (texts.Length > 1) texts[1].text = policy.description; // Описание

            var revokeBtn = item.GetComponentInChildren<Button>();
            if (revokeBtn != null)
            {
                revokeBtn.onClick.AddListener(() => RevokePolicy(policy.id));
            }
        }

        private void RevokePolicy(string id)
        {
            PolicyManager.Instance.DeactivatePolicy(id);
            TeletypeManager.Instance?.Log($"УКАЗ ОТМЕНЕН: {PolicyManager.Instance.GetPolicyById(id)?.displayName}");
            
            // Убираем одну визуальную бумажку с доски
            if (linkedBoard != null) linkedBoard.RemoveVisualDoc();

            RefreshList();
        }
    }
}