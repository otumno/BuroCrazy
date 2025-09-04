using UnityEngine;
using TMPro; // Для работы с TextMeshPro
using UnityEngine.UI; // Для работы с кнопками

public class SaveSlotUI : MonoBehaviour
{
    [Header("UI элементы")]
    public TextMeshProUGUI infoText; // Текст "День 5, $1234" или "Пустой слот"
    public Button loadOrNewGameButton;
    public Button deleteButton;

    private int slotIndex;
    private MenuManager menuManager;
    private SaveLoadManager saveLoadManager;

    public void Setup(int index, MenuManager manager, SaveLoadManager saveManager)
    {
        slotIndex = index;
        menuManager = manager;
        saveLoadManager = saveManager;

        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        SaveData data = saveLoadManager.GetDataForSlot(slotIndex);

        if (data != null) // Если сохранение есть
        {
            infoText.text = $"День: {data.day}\nСчет: ${data.money}";
            loadOrNewGameButton.GetComponentInChildren<TextMeshProUGUI>().text = "Загрузить";
            deleteButton.gameObject.SetActive(true);

            // Настраиваем кнопки
            loadOrNewGameButton.onClick.RemoveAllListeners();
            loadOrNewGameButton.onClick.AddListener(() => menuManager.OnSaveSlotClicked(slotIndex));

            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnDeleteClicked);
        }
        else // Если сохранения нет
        {
            infoText.text = "Пустой слот";
            loadOrNewGameButton.GetComponentInChildren<TextMeshProUGUI>().text = "Новая игра";
            deleteButton.gameObject.SetActive(false);

            // Настраиваем кнопки
            loadOrNewGameButton.onClick.RemoveAllListeners();
            loadOrNewGameButton.onClick.AddListener(() => menuManager.OnNewGameClicked(slotIndex));
        }
    }

    private void OnDeleteClicked()
    {
        // TODO: Сделать окно подтверждения "Вы уверены?"
        saveLoadManager.DeleteSave(slotIndex);
        UpdateVisuals(); // Обновляем UI, чтобы показать "Пустой слот"
    }
}