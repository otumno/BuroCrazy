// Файл: UI/ActionDropZone.cs --- ФИНАЛЬНАЯ ВЕРСИЯ ---
using UnityEngine;
using UnityEngine.EventSystems;

public class ActionDropZone : MonoBehaviour, IDropHandler
{
    public enum ZoneType { Available, Active }
    public ZoneType type;
    public ActionConfigPopupUI popupController;

    // --- НОВОЕ ПОЛЕ: Ссылка на контейнер, КУДА нужно помещать иконки ---
    public Transform contentContainer;

    public void OnDrop(PointerEventData eventData)
    {
        // Проверяем, что все ссылки на месте, включая новую
        if (popupController == null || contentContainer == null) return;

        ActionIconUI icon = eventData.pointerDrag.GetComponent<ActionIconUI>();
        if (icon != null)
        {
            if (this.type == ZoneType.Active && !popupController.CanAddAction())
            {
                return;
            }

            // --- ИЗМЕНЕНИЕ: Теперь мы делаем иконку дочерней для ПРАВИЛЬНОГО контейнера ---
            icon.transform.SetParent(contentContainer);
            
            popupController.OnActionDropped(icon.actionData, this.type);
        }
    }
}