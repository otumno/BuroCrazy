using UnityEngine;
using UnityEngine.UI;
using System.Linq;

// Этот скрипт нужно повесить на саму кнопку "Бухгалтерия"
[RequireComponent(typeof(Button))]
public class BookkeepingButtonController : MonoBehaviour
{
    private Button bookkeepingButton;
    private BookkeepingPanelUI bookkeepingPanel; 

    private void Awake()
    {
        bookkeepingButton = GetComponent<Button>();
        bookkeepingButton.onClick.AddListener(OnButtonClick);
    }

    private void Start()
    {
        // Находим панель один раз при старте (она может быть выключена)
        bookkeepingPanel = FindObjectOfType<BookkeepingPanelUI>(true);

        // Кнопка изначально неактивна (невидима или некликабельна)
        bookkeepingButton.interactable = false;
    }

    void Update()
{
    if (HiringManager.Instance == null) return;

    // Проверяем, есть ли хотя бы один сотрудник с действием "Ведение бухгалтерии"
    bool isUnlocked = HiringManager.Instance.AllStaff
        .Any(staff => staff.activeActions.Any(action => action.actionType == ActionType.DoBookkeeping));

    // Теперь активность кнопки зависит ТОЛЬКО от того, разблокирована ли функция
    bookkeepingButton.interactable = isUnlocked;
}

    private void OnButtonClick()
    {
        if (bookkeepingPanel != null)
        {
            bookkeepingPanel.Show();
        }
    }
}