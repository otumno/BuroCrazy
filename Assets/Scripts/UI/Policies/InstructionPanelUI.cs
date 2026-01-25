using UnityEngine;

namespace UI.Policies
{
    public class InstructionPanelUI : MonoBehaviour
    {
        [Header("Панель")]
        public GameObject panelObject;
        public Transform instructionsContainer;
        public GameObject instructionPrefab;

        [Header("Кнопка на столе директора")]
        public UnityEngine.UI.Button deskButton;
        public GameObject deskButtonHighlight;

        [Header("Доска в мире")]
        public GameObject worldNoticeBoard;
        public GameObject worldBoardPanel;
        public UnityEngine.UI.Button worldBoardButton;

        [Header("Ссылка на менеджер")]
        public Managers.InstructionManager instructionManager;

        private void Start()
        {
            if (deskButton != null)
            {
                deskButton.onClick.AddListener(TogglePanel);
            }

            if (worldBoardButton != null)
            {
                worldBoardButton.onClick.AddListener(TogglePanel);
            }

            panelObject?.SetActive(false);
            worldBoardPanel?.SetActive(false);
            deskButtonHighlight?.SetActive(false);

            if (instructionManager == null)
            {
                instructionManager = Managers.InstructionManager.Instance;
            }
        }

        public void TogglePanel()
        {
            bool isActive = panelObject != null && panelObject.activeSelf;

            HidePanel();

            if (!isActive)
            {
                ShowPanel();
            }
        }

        public void ShowPanel()
        {
            panelObject?.SetActive(true);
            worldBoardPanel?.SetActive(true);
            deskButtonHighlight?.SetActive(false);

            RefreshInstructions();
        }

        public void HidePanel()
        {
            panelObject?.SetActive(false);
            worldBoardPanel?.SetActive(false);
        }

        public void RefreshInstructions()
        {
            if (instructionsContainer == null || instructionPrefab == null || instructionManager == null)
            {
                return;
            }

            foreach (Transform child in instructionsContainer)
            {
                Destroy(child.gameObject);
            }

            var instructions = instructionManager.GetAllEnabledInstructions();

            foreach (var instruction in instructions)
            {
                CreateInstructionItem(instruction);
            }
        }

        private void CreateInstructionItem(Data.Policies.JobInstruction instruction)
        {
            var itemObj = Instantiate(instructionPrefab, instructionsContainer);
            var itemUI = itemObj.GetComponent<InstructionItemUI>();

            if (itemUI != null)
            {
                itemUI.Setup(instruction, instructionManager);
            }
        }

        private void Update()
        {
            if (deskButtonHighlight != null && instructionManager != null)
            {
                bool hasNewInstructions = instructionManager.GetAllEnabledInstructions().Count > 0;
                deskButtonHighlight.SetActive(hasNewInstructions);
            }
        }
    }
}
