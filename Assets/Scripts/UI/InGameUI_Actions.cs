// Файл: InGameUI_Actions.cs
using UnityEngine;

public class InGameUI_Actions : MonoBehaviour
{
	[Header("Действия Директора")]
    public StaffAction directorPrepareSalariesAction;
	
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
	
	public void OnPrepareSalariesButtonClick()
    {
        var director = DirectorAvatarController.Instance;
        if (director != null && directorPrepareSalariesAction != null)
        {
            // Принудительно запускаем выполнение действия у Директора
            director.ExecuteAction(directorPrepareSalariesAction);
        }
    }
	
}