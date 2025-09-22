// Файл: ActionDropZone.cs
using UnityEngine;
using UnityEngine.EventSystems;

public class ActionDropZone : MonoBehaviour, IDropHandler
{
    // Этот метод вызывается, когда на объект с этим скриптом "бросают" другой объект
    public void OnDrop(PointerEventData eventData)
    {
        GameObject droppedObject = eventData.pointerDrag; // Получаем объект, который мы тащили
        ActionIconUI icon = droppedObject.GetComponent<ActionIconUI>();

        if (icon != null)
        {
            // Устанавливаем родителя иконки на этот объект (наш контейнер)
            icon.transform.SetParent(this.transform);
        }
    }
}