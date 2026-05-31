using Managers;
using UnityEngine;
using UnityEngine.UI;
using Gameplay;
using CinematicSystem;


[RequireComponent(typeof(Button))]
public class ShowDirectorDeskButton : MonoBehaviour
{
    void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnShowDeskClicked);
    }


    private void OnShowDeskClicked()
    {
        // Проверяем, играет ли кинематограф и находится ли в режиме ожидания клика
        var cinematicPlayer = FindObjectOfType<CinematicPlayer>();
        if (cinematicPlayer != null && cinematicPlayer.IsPlaying && cinematicPlayer.CurrentGraph != null)
        {
            // Проверяем, является ли текущий граф туториалом первого дня
            // Если граф играет и мы停在 wait_click - это этап туториала
            // Ждём пока CinematicPlayer сам обработает клик через WaitForUIClickNode
            Debug.Log("[ShowDirectorDeskButton] Клик по столу во время кинематографа - передаём управление CinematicPlayer");
            
            // Кинематограф сам обработает клик через WaitForUIClickNode
            // Пока просто выходим - граф сам продолжится
            return;
        }
        
        // Старая логика (ShowDirectorDesk)
        if (MainUIManager.Instance != null)
        {
            MainUIManager.Instance.ShowDirectorDesk();
        }
    }
}
