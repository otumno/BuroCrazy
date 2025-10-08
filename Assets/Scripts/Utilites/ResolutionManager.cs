using UnityEngine;

public class ResolutionManager : MonoBehaviour
{
    void Start()
    {
        // Получаем текущие параметры экрана
        int currentWidth = Screen.width;
        int currentHeight = Screen.height;
        float currentAspect = (float)currentWidth / currentHeight;

        // Целевое соотношение сторон для Steam Deck (16:10)
        float steamDeckAspect = 16f / 10f;

        // Сравниваем с небольшой погрешностью
        if (Mathf.Abs(currentAspect - steamDeckAspect) < 0.05f)
        {
            Debug.Log("Обнаружен экран Steam Deck (16:10). Устанавливаем нативное разрешение.");
            // Устанавливаем нативное разрешение для Steam Deck
            Screen.SetResolution(1280, 800, FullScreenMode.FullScreenWindow);
        }
        else
        {
            Debug.Log($"Обнаружен экран {currentWidth}x{currentHeight}. Используем текущее разрешение.");
            // Для всех других экранов просто используем их текущее разрешение в полноэкранном режиме
            Screen.SetResolution(currentWidth, currentHeight, FullScreenMode.FullScreenWindow);
        }

        // Этот скрипт выполнил свою работу и больше не нужен
        Destroy(this);
    }
}