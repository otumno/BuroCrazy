// Файл: SceneEventSystemValidator.cs
using UnityEngine;
using UnityEngine.EventSystems;

public class SceneEventSystemValidator : MonoBehaviour
{
    void OnEnable()
    {
        // Проверяем, существует ли уже "бессмертный" EventSystem
        if (PersistentEventSystem.Instance != null && PersistentEventSystem.Instance != this.GetComponent<PersistentEventSystem>())
        {
            // Если да, и мы - не он, то мы - двойник. Уничтожаем себя.
            Debug.Log("Обнаружен дубликат EventSystem в сцене. Уничтожаю его.");
            Destroy(this.gameObject);
        }
    }
}