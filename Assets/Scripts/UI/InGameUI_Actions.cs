// Файл: InGameUI_Actions.cs
using UnityEngine;

public class InGameUI_Actions : MonoBehaviour
{
    public void OnRadioButtonClick()
    {
        MusicPlayer.Instance?.RequestNextTrack();
    }

    public void OnCabinetButtonClick()
    {
        MainUIManager.Instance?.ShowDirectorDesk();
    }

    public void OnMainMenuButtonClick()
    {
        MainUIManager.Instance?.GoToMainMenu();
    }

    // --- ДОБАВЛЕНО ---
    /// <summary>
    /// Вызывается при нажатии на кнопку "За стол".
    /// </summary>
    public void OnGoToDeskButtonClick()
    {
        // Находим экземпляр аватара директора и вызываем его метод GoToDesk()
        DirectorAvatarController.Instance?.GoToDesk();
    }
}