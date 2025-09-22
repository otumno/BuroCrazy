// Файл: ActionIconUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems; // Добавлено для работы с событиями мыши

// Добавляем интерфейсы для перетаскивания
public class ActionIconUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Ссылки на UI")]
    [SerializeField] private TextMeshProUGUI actionNameText;
    [SerializeField] private Image backgroundImage;

    public StaffAction actionData { get; private set; }
    
    // Переменные для процесса перетаскивания
    private Transform parentAfterDrag;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            // Добавляем CanvasGroup, если его нет. Он нужен для drag-and-drop.
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void Setup(StaffAction data)
    {
        // ... (этот метод без изменений)
    }

    // Вызывается в момент, когда мы "схватили" иконку
    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"Начато перетаскивание: {actionData.displayName}");
        parentAfterDrag = transform.parent; // Запоминаем, где иконка лежала
        transform.SetParent(transform.root); // Временно делаем иконку дочерней к Canvas, чтобы она была поверх всего
        transform.SetAsLastSibling(); // Убеждаемся, что она рисуется последней (поверх всех)
        canvasGroup.blocksRaycasts = false; // "Отключаем" иконку для мыши, чтобы можно было "увидеть", что под ней
    }

    // Вызывается в каждом кадре, пока мы тащим иконку
    public void OnDrag(PointerEventData eventData)
    {
        transform.position = Input.mousePosition; // Иконка следует за мышкой
    }

    // Вызывается в момент, когда мы "отпустили" иконку
    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"Завершено перетаскивание: {actionData.displayName}");
        transform.SetParent(parentAfterDrag); // Возвращаем иконку на ее последнее место
        canvasGroup.blocksRaycasts = true; // "Включаем" иконку для мыши обратно
    }
}