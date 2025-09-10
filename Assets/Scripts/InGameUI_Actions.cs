// Файл: InGameUI_Actions.cs
using UnityEngine;

public class InGameUI_Actions : MonoBehaviour
{
    // Метод для кнопки Радио
    public void OnRadioButtonClick()
    {
        // Находим "бессмертный" MusicPlayer и вызываем его метод
        if (MusicPlayer.Instance != null)
        {
            MusicPlayer.Instance.RequestNextTrack();
        }
    }

    // Метод для кнопки Кабинет/Пауза
    public void OnCabinetButtonClick()
    {
        // Находим "бессмертный" MenuManager и вызываем его метод
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.ToggleSimplePauseMenu();
        }
    }

    // Метод для кнопки "Главное меню" (которая на панели паузы)
    public void OnMainMenuButtonClick()
    {
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.OnBackToMainMenuClicked();
        }
    }
}