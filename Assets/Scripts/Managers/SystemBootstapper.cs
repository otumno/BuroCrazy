// Файл: Assets/Scripts/Managers/SystemBootstrapper.cs
using UnityEngine;

public class SystemsBootstrapper : MonoBehaviour
{
    public static SystemsBootstrapper Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Делаем бессмертным ЭТОТ ОБЪЕКТ ([SYSTEMS])
            // и всех его детей.
            DontDestroyOnLoad(this.gameObject);
            Debug.Log($"<color=cyan>[SystemsBootstrapper]</color> [SYSTEMS] сделан бессмертным.");
        }
        else if (Instance != this)
        {
            // Если [SYSTEMS] уже существует,
            // уничтожаем этот дубликат.
            Debug.LogWarning($"[SystemsBootstrapper] Обнаружен дубликат [SYSTEMS]. Уничтожаю его.");
            Destroy(this.gameObject);
        }
    }
}