// Файл: InGameUI_Actions.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class InGameUI_Actions : MonoBehaviour
{
	[Header("Действия Директора")]
    public StaffAction directorPrepareSalariesAction;
	
	[Header("Панели для открытия")] // Можно добавить такой заголовок для ясности
    [Tooltip("Перетащите сюда объект BookkeepingPanel из UI")]
    [SerializeField] private BookkeepingPanelUI bookkeepingPanelReference;
	
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
	
	public void OnViewBookkeepingClicked()
    {
        var director = DirectorAvatarController.Instance;
        var bookkeepingDesk = ScenePointsRegistry.Instance?.bookkeepingDesk;

        // --- ДОБАВИТЬ ПРОВЕРКУ ---
        if (bookkeepingPanelReference == null) // <<<< НОВАЯ ПРОВЕРКА
        {
            Debug.LogError("Невозможно посмотреть бухгалтерию: Ссылка на BookkeepingPanelUI не установлена в InGameUI_Actions!");
            director?.thoughtBubble?.ShowPriorityMessage("Ошибка интерфейса!", 3f, Color.red);
            return;
        }
        // --- КОНЕЦ ПРОВЕРКИ ---

        if (director == null || bookkeepingDesk == null || bookkeepingDesk.clerkStandPoint == null)
        {
            Debug.LogError("Невозможно посмотреть бухгалтерию: Директор или стол бухгалтера (или его точка) не найдены!");
            director?.thoughtBubble?.ShowPriorityMessage("Стол бухгалтера не найден!", 3f, Color.red);
            return;
        }

        // --- ИЗМЕНИТЬ ВЫЗОВ КОРУТИНЫ ---
        // Передаем bookkeepingPanelReference как аргумент
        director.StartCoroutine(director.GoAndViewBookkeeping(bookkeepingDesk.clerkStandPoint.position, bookkeepingPanelReference)); // <<<< ИЗМЕНЕНИЕ
    }
	
}