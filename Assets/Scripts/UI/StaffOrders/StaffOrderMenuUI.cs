using System.Collections.Generic;
using Managers;
using StaffOrders;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.StaffOrders
{
    /// <summary>
    /// Контекстное меню приказов по ПКМ на сотруднике.
    /// В шапке — портрет, имя и роль; ниже — приказы для его роли и пункт «Тактика…» со старым окном тактики.
    /// Пока меню открыто, игра на паузе.
    /// </summary>
    public class StaffOrderMenuUI : MonoBehaviour
    {
        [SerializeField] private StaffOrderCatalog catalog;

        [Header("Шапка")]
        [SerializeField] private StaffPortraitUI portrait;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI roleText;

        [Header("Список")]
        [Tooltip("Панель меню. Пивот — левый верхний угол: меню открывается вправо-вниз от курсора.")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private Transform itemsContainer;
        [SerializeField] private StaffOrderMenuItemUI itemPrefab;

        [Header("Закрытие")]
        [Tooltip("Прозрачная подложка на весь экран: клик мимо меню закрывает его.")]
        [SerializeField] private Button backgroundButton;

        [Header("Связи")]
        [SerializeField] private ActionConfigPopupUI tacticsPopup;

        private readonly List<StaffOrderMenuItemUI> _items = new List<StaffOrderMenuItemUI>();
        private StaffController _staff;
        private int _openedFrame;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (backgroundButton != null) backgroundButton.onClick.AddListener(Close);
            if (tacticsPopup == null) tacticsPopup = FindFirstObjectByType<ActionConfigPopupUI>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Сотрудник ушёл домой или удалён, пока меню было открыто.
            if (_staff == null || !_staff.gameObject.activeInHierarchy)
            {
                Close();
                return;
            }

            // ПКМ, которым меню открыли, приходит и сюда в том же кадре — его пропускаем.
            if (Time.frameCount == _openedFrame) return;

            // ПКМ мимо меню тоже закрывает его: подложка-кнопка ловит только левый клик.
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                Close();
            }
        }

        public void Open(StaffController staff, Vector2 screenPosition)
        {
            if (staff == null || !staff.IsOnDuty() || !staff.gameObject.activeInHierarchy) return;
            if (catalog == null)
            {
                Debug.LogError("[StaffOrderMenuUI] Не назначен каталог приказов (StaffOrderCatalog).");
                return;
            }

            if (IsOpen) Close();

            _staff = staff;
            _openedFrame = Time.frameCount;
            gameObject.SetActive(true);
            IsOpen = true;
            MainUIManager.Instance?.PushPause();

            FillHeader();
            RebuildItems();
            PlaceAt(screenPosition);
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            _staff = null;
            gameObject.SetActive(false);
            MainUIManager.Instance?.PopPause();
        }

        private void OnDisable()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _staff = null;
            MainUIManager.Instance?.PopPause();
        }

        private void FillHeader()
        {
            if (portrait != null) portrait.Show(_staff);
            if (nameText != null) nameText.text = _staff.characterName;
            if (roleText != null)
            {
                var label = roleText.GetComponent<LocalizedText>() ?? roleText.gameObject.AddComponent<LocalizedText>();
                label.SetReference(GameText.Table, StaffRoleNames.GetKey(_staff.currentRole));
            }
        }

        private void RebuildItems()
        {
            foreach (var item in _items)
            {
                if (item != null) Destroy(item.gameObject);
            }
            _items.Clear();

            if (itemPrefab == null || itemsContainer == null) return;

            foreach (var definition in catalog.GetOrdersFor(_staff))
            {
                // Невыполнимый сейчас приказ показываем серым, а не прячем: игрок видит, что такой приказ вообще есть.
                bool canBeOrdered = definition.Action.CanBeOrdered(_staff);
                AddItem(definition.Label, definition.LabelKey, canBeOrdered, () => GiveOrder(definition));
            }

            if (tacticsPopup != null)
            {
                AddItem(null, "order.tactics", true, OpenTactics);
            }
        }

        private void AddItem(string label, string key, bool isInteractable, System.Action onClick)
        {
            var item = Instantiate(itemPrefab, itemsContainer);
            item.Setup(label, isInteractable, onClick, key);
            _items.Add(item);
        }

        private void GiveOrder(StaffOrderDefinition definition)
        {
            var staff = _staff;
            if (staff == null || !staff.isActiveAndEnabled || !staff.IsOnDuty() ||
                !definition.IsAvailableFor(staff) || !definition.Action.CanBeOrdered(staff))
            {
                Close();
                return;
            }
            var order = catalog.CreateOrder(definition);
            Close();

            if (DirectorAvatarController.Instance != null)
            {
                DirectorAvatarController.Instance.GiveOrder(staff, order);
            }
            else
            {
                staff.ReceiveOrder(order);
            }
        }

        private void OpenTactics()
        {
            var staff = _staff;

            tacticsPopup.OpenForStaff(staff);
            Close();
        }

        private void PlaceAt(Vector2 screenPosition)
        {
            if (panel == null) return;

            var parent = panel.parent as RectTransform;
            if (parent == null) return;

            var canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, eventCamera, out var localPoint))
            {
                return;
            }

            // Размер панели зависит от числа пунктов, поэтому сначала пересчитываем раскладку, потом прижимаем к краям.
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            Rect bounds = parent.rect;
            Vector2 size = panel.rect.size;
            localPoint.x = Mathf.Clamp(localPoint.x, bounds.xMin, bounds.xMax - size.x);
            localPoint.y = Mathf.Clamp(localPoint.y, bounds.yMin + size.y, bounds.yMax);

            panel.localPosition = localPoint;
        }
    }
}
