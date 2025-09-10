using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// Этот скрипт нужно повесить на ваш главный Canvas
public class UIRaycastDebugger : MonoBehaviour
{
    void Update()
    {
        // Проверяем, была ли нажата левая кнопка мыши в этом кадре
        if (Input.GetMouseButtonDown(0))
        {
            // Создаем объект для хранения данных о курсоре
            PointerEventData pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = Input.mousePosition;

            List<RaycastResult> results = new List<RaycastResult>();

            // Пускаем луч во все UI-элементы на сцене
            EventSystem.current.RaycastAll(pointerData, results);

            // Если луч во что-то попал
            if (results.Count > 0)
            {
                // Выводим в консоль имя САМОГО ВЕРХНЕГО объекта под курсором.
                // Второй аргумент (results[0].gameObject) позволит по клику на лог подсветить этот объект в иерархии.
                Debug.Log($"Клик попал в: {results[0].gameObject.name}", results[0].gameObject);
            }
            else
            {
                Debug.Log("Клик попал в пустоту (не в UI).");
            }
        }
    }
}