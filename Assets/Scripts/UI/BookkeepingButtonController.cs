using UnityEngine;
using UnityEngine.UI;
using System.Linq;

// Этот скрипт нужно повесить на саму кнопку "Бухгалтерия"
[RequireComponent(typeof(Button))]
public class BookkeepingButtonController : MonoBehaviour
{
    private Button bookkeepingButton;
    private BookkeepingPanelUI bookkeepingPanel; 

    void Awake()
    {
        bookkeepingButton = GetComponent<Button>();
        bookkeepingButton.onClick.AddListener(OnButtonClick);
    }

    void Start()
    {
        // Находим панель один раз при старте (она может быть выключена)
        bookkeepingPanel = FindFirstObjectByType<BookkeepingPanelUI>(FindObjectsInactive.Include);

        // Проверяем состояние кнопки при старте
        UpdateButtonState();
    }

    void Update()
    {
        // Проверяем состояние кнопки каждый кадр, чтобы она появилась сразу после назначения действия
        UpdateButtonState();
    }

    // --- ГЛАВНЫЙ МЕТОД ПРОВЕРКИ ---
    void UpdateButtonState()
    {
        if (HiringManager.Instance == null) return;

        // Проверяем, есть ли ХОТЯ БЫ ОДИН сотрудник в общем списке,
        // у которого в списке активных действий есть "Ведение бухгалтерии".
        bool isUnlocked = HiringManager.Instance.AllStaff
            .Any(staff => staff.activeActions.Any(action => action.actionType == ActionType.DoBookkeeping));

        // Включаем или выключаем саму кнопку (ее видимость), если состояние изменилось
        if (gameObject.activeSelf != isUnlocked)
        {
            gameObject.SetActive(isUnlocked);
        }
    }

    private void OnButtonClick()
    {
        // При нажатии - показываем панель
        if (bookkeepingPanel != null)
        {
            bookkeepingPanel.Show();
        }
    }
}