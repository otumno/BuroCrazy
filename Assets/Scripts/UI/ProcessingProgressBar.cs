using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ProcessingProgressBar : MonoBehaviour
{
    public Slider slider;
    public Canvas canvas;

    // Метод настройки и запуска
    public IEnumerator RunProgress(Vector3 startPos, Vector3 endPos, float duration)
    {
        // 1. Позиционирование
        // Ставим бар ровно посередине между Клерком и Клиентом, но чуть выше (y + 1.5)
        Vector3 centerPos = (startPos + endPos) / 2f;
        centerPos.y += 1.5f; 
        transform.position = centerPos;

        // Настройка Canvas (чтобы смотрел в камеру)
        if (canvas != null)
        {
            canvas.worldCamera = Camera.main;
            canvas.sortingLayerName = "UI";
            canvas.sortingOrder = 500; // Поверх всего
        }

        // 2. Анимация заполнения
        float timer = 0f;
        slider.value = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            slider.value = timer / duration;
            yield return null;
        }

        slider.value = 1f;
        
        // 3. Уничтожение после завершения
        Destroy(gameObject);
    }
}