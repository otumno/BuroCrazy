// Файл: StartOfDayPanel.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartOfDayPanel : MonoBehaviour
{
    [Header("UI Компоненты")]
    public TextMeshProUGUI dayInfoText;
    public Button startDayButton;
    public Button hireButton;
    
    [Header("Ссылки на менеджеры")]
    public MenuManager menuManager;
    public DirectorManager directorManager;

    private void OnEnable()
    {
        UpdatePanelInfo();
    }

    private void Start()
    {
        startDayButton.onClick.RemoveAllListeners();
        startDayButton.onClick.AddListener(OnStartOrContinueClicked);
    }

    public void UpdatePanelInfo()
    {
        if (directorManager == null || ClientSpawner.Instance == null) return;

        string dayInfo = $"День: {ClientSpawner.Instance.GetCurrentDay()}\n";
        dayInfo += $"Страйки: {directorManager.currentStrikes}/3\n";
        
        // Эта логика определяет, какая надпись будет на кнопке
        if (directorManager.activeOrders.Count > 0)
        {
            DirectorOrder activeOrder = directorManager.activeOrders[0];
            dayInfo += $"\n<b>Активный приказ:</b>\n<color=yellow>{activeOrder.orderName}</color>";
            startDayButton.GetComponentInChildren<TextMeshProUGUI>().text = "Продолжить день";
        }
        else
        {
            dayInfo += "\n<color=red>Требуется издать приказ на день!</color>";
            startDayButton.GetComponentInChildren<TextMeshProUGUI>().text = "Начать день";
        }

        dayInfoText.text = dayInfo;
        startDayButton.interactable = true;
    }
    
    // --- КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ ЗДЕСЬ ---
    private void OnStartOrContinueClicked()
    {
        if (menuManager == null) return;

        // Используем ту же самую проверку, что и для смены надписи на кнопке
        if (directorManager.activeOrders.Count > 0)
        {
            // Если приказ активен, значит, мы нажимаем "Продолжить день".
            // Вызываем ПРОСТОЙ метод для закрытия меню паузы (без сохранения и парада).
            menuManager.ToggleSimplePauseMenu();
        }
        else 
        {
            // Если приказа нет, мы нажимаем "Начать день".
            // Вызываем ПОЛНЫЙ метод для старта дня (который покажет приказы).
            menuManager.OnStartDayClicked();
        }
    }
}