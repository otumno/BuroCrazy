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
        // Используем правильный метод из нового MenuManager (который живет в GameScene)
        MainUIManager.Instance?.ShowPausePanel(true);
    }

    public void OnMainMenuButtonClick()
    {
        // Используем правильный метод из нового MenuManager (который живет в GameScene)
        MainUIManager.Instance?.GoToMainMenu();
    }
}