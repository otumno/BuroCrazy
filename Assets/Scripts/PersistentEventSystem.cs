// Файл: PersistentEventSystem.cs - ФИНАЛЬНАЯ ВЕРСИЯ
using UnityEngine;
using UnityEngine.EventSystems;

public class PersistentEventSystem : MonoBehaviour
{
    public static PersistentEventSystem Instance;

    private void Awake()
    {
        if (Instance != null)
        {
            // Если "бессмертный" экземпляр уже существует, 
            // значит, мы - двойник из новой сцены. Уничтожаем себя.
            Destroy(gameObject);
        }
        else
        {
            // Если мы первые, становимся "бессмертным" экземпляром.
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}