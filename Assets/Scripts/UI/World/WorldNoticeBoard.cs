using UnityEngine;

namespace UI.World
{
    public class WorldNoticeBoard : MonoBehaviour
    {
        [Header("UI Panel")]
        public GameObject infoPanel;
        public TMPro.TextMeshProUGUI titleText;
        public TMPro.TextMeshProUGUI contentText;

        [Header("Интерактивность")]
        public UnityEngine.UI.Button openButton;
        public UnityEngine.UI.Button closeButton;
        public UnityEngine.Collider2D interactionCollider;

        [Header("Отображение в мире")]
        public GameObject visualRepresentation;
        public Sprite normalSprite;
        public Sprite activeSprite;

        private SpriteRenderer spriteRenderer;
        private bool isOpen = false;

        private void Awake()
        {
            spriteRenderer = visualRepresentation?.GetComponent<SpriteRenderer>();

            if (openButton != null)
            {
                openButton.onClick.AddListener(OpenPanel);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(ClosePanel);
            }

            if (infoPanel != null)
            {
                infoPanel.SetActive(false);
            }
        }

        private void OnMouseDown()
        {
            if (interactionCollider != null && interactionCollider.OverlapPoint(Input.mousePosition))
            {
                TogglePanel();
            }
        }

        public void TogglePanel()
        {
            if (isOpen)
            {
                ClosePanel();
            }
            else
            {
                OpenPanel();
            }
        }

        public void OpenPanel()
        {
            isOpen = true;
            infoPanel?.SetActive(true);

            if (spriteRenderer != null && activeSprite != null)
            {
                spriteRenderer.sprite = activeSprite;
            }

            UpdateContent();
        }

        public void ClosePanel()
        {
            isOpen = false;
            infoPanel?.SetActive(false);

            if (spriteRenderer != null && normalSprite != null)
            {
                spriteRenderer.sprite = normalSprite;
            }
        }

        private void UpdateContent()
        {
            if (Managers.InstructionManager.Instance == null)
            {
                if (titleText != null) titleText.text = "Доска объявлений";
                if (contentText != null) contentText.text = "Нет активных инструкций.";
                return;
            }

            var instructions = Managers.InstructionManager.Instance.GetAllEnabledInstructions();

            if (titleText != null)
            {
                titleText.text = $"Доска объявлений ({instructions.Count})";
            }

            if (contentText != null)
            {
                if (instructions.Count == 0)
                {
                    contentText.text = "Нет активных инструкций.";
                }
                else
                {
                    contentText.text = "Активные инструкции:\n\n";
                    foreach (var instr in instructions)
                    {
                        contentText.text += $"• {instr.displayName}\n";
                    }
                }
            }
        }

        private void Update()
        {
            if (Managers.InstructionManager.Instance != null)
            {
                var instructions = Managers.InstructionManager.Instance.GetAllEnabledInstructions();
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = instructions.Count > 0 ? activeSprite : normalSprite;
                }
            }
        }
    }
}
