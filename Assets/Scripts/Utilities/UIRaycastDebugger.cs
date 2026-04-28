using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// Этот скрипт нужно повесить на ваш главный Canvas
namespace Utilities
{
    public class UIRaycastDebugger : MonoBehaviour
    {
        private void Update()
        {
            // Закомментировано для уменьшения спама в консоли
            // if (Input.GetMouseButtonDown(0))
            // {
            //     PointerEventData pointerData = new PointerEventData(EventSystem.current);
            //     pointerData.position = Input.mousePosition;
            //
            //     List<RaycastResult> results = new List<RaycastResult>();
            //
            //     EventSystem.current.RaycastAll(pointerData, results);
            //
            //     if (results.Count > 0)
            //     {
            //         Debug.Log($"Клик попал в: {results[0].gameObject.name}", results[0].gameObject);
            //     }
            //     else
            //     {
            //         Debug.Log("Клик попал в пустоту (не в UI).");
            //     }
            // }
        }
    }
}