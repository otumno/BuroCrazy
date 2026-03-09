// Файл: UI/ActionIconUI.cs --- НОВАЯ ВЕРСИЯ ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class ActionIconUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private TextMeshProUGUI actionNameText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button forceButton; // Кнопка приказа
    public StaffAction actionData { get; private set; }
    
    // Делегат для обработки нажатия кнопки приказа
    private System.Action<StaffAction> onForceActionClicked;
    
    private CanvasGroup canvasGroup;
    private Transform parentBeforeDrag; // Переменная для запоминания "дома"

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        
        // Подписываемся на нажатие кнопки приказа
        if (forceButton != null)
        {
            forceButton.onClick.AddListener(OnForceButtonClicked);
        }
    }

    private void OnForceButtonClicked()
    {
        // Вызываем callback с данным действия
        onForceActionClicked?.Invoke(actionData);
    }

    public void Setup(StaffAction data, bool showForceButton = false, System.Action<StaffAction> forceCallback = null)
    {
        this.actionData = data;
        if (actionNameText != null) { actionNameText.text = data.displayName; }
        
        // Сохраняем callback
        onForceActionClicked = forceCallback;
        
        // Показываем/скрываем кнопку приказа
        if (forceButton != null)
        {
            forceButton.gameObject.SetActive(showForceButton);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = false;
        parentBeforeDrag = transform.parent; // Запоминаем, откуда нас взяли
        transform.SetParent(GetComponentInParent<Canvas>().transform, true);
        transform.SetAsLastSibling();
        transform.localScale = Vector3.one;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        // Если после перетаскивания наш родитель все еще Canvas (т.е. мы не попали в DropZone)
        if (transform.parent == parentBeforeDrag.GetComponentInParent<Canvas>().transform)
        {
            // ...то мы возвращаемся "домой".
            transform.SetParent(parentBeforeDrag);
        }
    }
}