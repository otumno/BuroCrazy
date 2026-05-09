using Managers;
using UnityEngine;
using UnityEngine.UI;
using Gameplay;


[RequireComponent(typeof(Button))]
public class ShowDirectorDeskButton : MonoBehaviour
{
    void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnShowDeskClicked);
    }


    private void OnShowDeskClicked()
    {
        // Проверяем, находится ли игра в режиме туториала и ждёт клика по столу
        var tutorial = FindObjectOfType<FirstDayTutorial>();
        if (tutorial != null && tutorial.IsWaitingForDeskClick)
        {
            tutorial.OnDeskButtonClickedDuringTutorial();
            return;
        }
        
        // Старая логика (ShowDirectorDesk)
        if (MainUIManager.Instance != null)
        {
            MainUIManager.Instance.ShowDirectorDesk();
        }
    }
}
