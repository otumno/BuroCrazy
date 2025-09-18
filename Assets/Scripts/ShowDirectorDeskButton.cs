using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ShowDirectorDeskButton : MonoBehaviour
{
    void Start()
    {
        Button thisButton = GetComponent<Button>();

        // Привязываем нажатие кнопки к нашему новому методу
        thisButton.onClick.AddListener(OnShowDeskClicked);
    }

    private void OnShowDeskClicked()
    {
        // 1. Находим панель стола директора в сцене
        StartOfDayPanel deskPanel = FindFirstObjectByType<StartOfDayPanel>();

        if (deskPanel != null)
        {
            // 2. Запускаем ее корутину плавного появления,
            // делая ее интерактивной после завершения анимации.
            StartCoroutine(deskPanel.Fade(true, true));
            
            // 3. Дополнительно: можно включить музыку кабинета здесь же
            if (MusicPlayer.Instance != null)
            {
                MusicPlayer.Instance.PlayDirectorsOfficeTheme();
            }
        }
        else
        {
            Debug.LogError("[ShowDirectorDeskButton] Не удалось найти StartOfDayPanel в сцене!", this);
        }
    }
}