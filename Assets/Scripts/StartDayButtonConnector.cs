using UnityEngine;
using UnityEngine.UI;

// Эта строка гарантирует, что на объекте всегда будет компонент Button
[RequireComponent(typeof(Button))]
public class StartDayButtonConnector : MonoBehaviour
{
    void Start()
    {
        // 1. Получаем компонент Button на этом же объекте
        Button startDayButton = GetComponent<Button>();

        // 2. Проверяем, существует ли наш "бессмертный" MainUIManager
        if (MainUIManager.Instance != null)
        {
            // 3. Программно "привязываем" нажатие этой кнопки к методу в MainUIManager.
            // Мы говорим: "Эй, кнопка, когда на тебя нажмут, вызови StartOrResumeGameplay из MainUIManager".
            startDayButton.onClick.AddListener(MainUIManager.Instance.StartOrResumeGameplay);
        }
        else
        {
            // Эта ошибка появится, если MainUIManager по какой-то причине не загрузился
            Debug.LogError("[StartDayButtonConnector] MainUIManager.Instance не найден! Не могу привязать кнопку.", this);
        }
    }
}