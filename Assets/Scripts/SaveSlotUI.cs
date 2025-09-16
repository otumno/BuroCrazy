using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotUI : MonoBehaviour
{
    [SerializeField] private int slotIndex;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private TextMeshProUGUI infoText;

    public void Setup(int index)
    {
        slotIndex = index;

        // Проверяем, что все ссылки на кнопки и текст установлены в префабе
        if (continueButton == null || newGameButton == null || deleteButton == null || infoText == null)
        {
            Debug.LogError($"[SaveSlotUI #{slotIndex}] Ошибка! Не все поля (кнопки, текст) назначены в инспекторе префаба!");
            return;
        }

        bool slotInUse = SaveLoadManager.Instance.DoesSaveExist(slotIndex);
        Debug.Log($"[SaveSlotUI #{slotIndex}] Слот используется: {slotInUse}");

        if (slotInUse)
        {
            SaveData data = SaveLoadManager.Instance.GetDataForSlot(slotIndex);
            continueButton.gameObject.SetActive(true);
            newGameButton.gameObject.SetActive(false);
            deleteButton.gameObject.SetActive(true);
            
            infoText.text = (data != null) ? $"День: {data.day}\nДеньги: ${data.money}" : "Ошибка чтения данных";
        }
        else
        {
            continueButton.gameObject.SetActive(false);
            newGameButton.gameObject.SetActive(true);
            deleteButton.gameObject.SetActive(false);
            infoText.text = "Пустой слот";
        }

        continueButton.onClick.RemoveAllListeners();
        newGameButton.onClick.RemoveAllListeners();
        deleteButton.onClick.RemoveAllListeners();

        continueButton.onClick.AddListener(() => MainUIManager.Instance.OnSaveSlotClicked(slotIndex));
        newGameButton.onClick.AddListener(() => MainUIManager.Instance.OnNewGameClicked(slotIndex));
        deleteButton.onClick.AddListener(() =>
        {
            SaveLoadManager.Instance.DeleteSave(slotIndex);
            FindFirstObjectByType<SaveLoadPanelController>()?.RefreshSlots();
        });
    }
}