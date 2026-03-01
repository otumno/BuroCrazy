// Assets/Scripts/UI/Policies/PolicyDeskButton.cs
using UnityEngine;
using UnityEngine.UI;

namespace UI.Policies
{
    // УБРАЛИ требование Button, теперь требуем Image (для цвета) и DeskInteractiveItem (для кликов)
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(DeskInteractiveItem))] 
    public class PolicyDeskButton : MonoBehaviour
    {
        [Header("UI")]
        public Image buttonImage; // Ссылка на картинку самой книги
        public GameObject notificationBadge; // Красный круг

        [Header("Настройки")]
        public Color normalColor = Color.white;
        public Color highlightColor = new Color(1f, 1f, 0.9f); // Чуть светлее, а не желтый

        private DeskInteractiveItem interactItem;
        private bool hasNewContent = false;

        private void Awake()
        {
            // Получаем ваш кастомный кликабельный компонент
            interactItem = GetComponent<DeskInteractiveItem>();
            
            // Если картинка не назначена, берем с текущего объекта
            if (buttonImage == null) buttonImage = GetComponent<Image>();
        }

        private void Start()
        {
            // Подписываемся на событие клика в DeskInteractiveItem
            if (interactItem != null)
            {
                interactItem.OnClick.AddListener(OnDeskItemClicked);
            }

            UpdateVisuals();

            if (Managers.UpgradeManager.Instance != null)
            {
                Managers.UpgradeManager.Instance.OnUpgradePurchased += CheckForNewContent;
            }
        }

        private void OnDestroy()
        {
            if (Managers.UpgradeManager.Instance != null)
            {
                Managers.UpgradeManager.Instance.OnUpgradePurchased -= CheckForNewContent;
            }
            
            if (interactItem != null)
            {
                interactItem.OnClick.RemoveListener(OnDeskItemClicked);
            }
        }

        // Этот метод вызывается, когда DeskInteractiveItem фиксирует клик
        private void OnDeskItemClicked()
        {
            var policyUI = FindFirstObjectByType<UI.PolicyBookUI>(FindObjectsInactive.Include);
            if (policyUI != null)
            {
                // Проверяем видимость через CanvasGroup, а не через activeSelf
                var cg = policyUI.GetComponent<CanvasGroup>();
                bool isVisible = cg != null && cg.alpha > 0.01f;


                if (isVisible)
                {
                    policyUI.Hide();
                }
                else
                {
                    policyUI.Show();
                }
            }
            else
            {
                Debug.LogError("PolicyBookUI не найдена на сцене!");
            }
        }

        private void CheckForNewContent()
        {
            hasNewContent = true;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (notificationBadge != null)
            {
                notificationBadge.SetActive(hasNewContent);
            }

            if (buttonImage != null)
            {
                buttonImage.color = hasNewContent ? highlightColor : normalColor;
            }
        }

        public void ClearNotification()
        {
            hasNewContent = false;
            UpdateVisuals();
        }
    }
}