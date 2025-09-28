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
    public StaffAction actionData { get; private set; }
    
    private CanvasGroup canvasGroup;
    private Transform parentBeforeDrag; // Переменная для запоминания "дома"

    private void Awake() { canvasGroup = GetComponent<CanvasGroup>(); }

    public void Setup(StaffAction data)
    {
        this.actionData = data;
        if (actionNameText != null) { actionNameText.text = data.displayName; }
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