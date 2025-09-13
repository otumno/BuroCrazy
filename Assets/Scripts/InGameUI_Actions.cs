// Файл: InGameUI_Actions.cs
using UnityEngine;

public class InGameUI_Actions : MonoBehaviour
{
    // Метод для кнопки Радио
    public void OnRadioButtonClick()
    {
        if (MusicPlayer.Instance != null)
        {
            MusicPlayer.Instance.RequestNextTrack();
        }
    }

    // Метод для кнопки Кабинет/Пауза
public void OnCabinetButtonClick()
{
    if (MenuManager.Instance != null)
    {
        MenuManager.Instance.OnCabinetButtonClick();
    }
}

    // Метод для кнопки "Главное меню" (которая на панели паузы)
public void OnMainMenuButtonClick()
{
    if (MenuManager.Instance != null)
    {
        MenuManager.Instance.GoToMainMenu();
    }
}

    // Метод для кнопки "К столу"
    public void OnGoToDeskButtonClick()
    {
        if (DirectorAvatarController.Instance != null)
        {
            DirectorAvatarController.Instance.GoToDesk();
        }
    }
}