// Файл: ShowDirectorDeskButton.cs

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ShowDirectorDeskButton : MonoBehaviour
{
    void Start()
    {
        Button thisButton = GetComponent<Button>();
        thisButton.onClick.AddListener(OnShowDeskClicked);
    }

    private void OnShowDeskClicked()
    {
        // --- ИЗМЕНЕНИЕ ЗДЕСЬ ---
        // Ищем панель, даже если она неактивна
        StartOfDayPanel deskPanel = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
        // ----------------------

        if (deskPanel != null)
        {
            StartCoroutine(deskPanel.Fade(true, true));
            
            if (MusicPlayer.Instance != null)
            {
                MusicPlayer.Instance.PlayDirectorsOfficeTheme();
            }
        }
        else
        {
            // И эта ошибка тоже больше не должна появляться
            Debug.LogError("[ShowDirectorDeskButton] Не удалось найти StartOfDayPanel в сцене!", this);
        }
    }
}