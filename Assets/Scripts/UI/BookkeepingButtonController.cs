// Файл: Assets/Scripts/UI/BookkeepingButtonController.cs
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Text; // Добавлено для логов

[RequireComponent(typeof(Button))]
public class BookkeepingButtonController : MonoBehaviour
{
    private Button bookkeepingButton;
    private BookkeepingPanelUI bookkeepingPanel; 
    private float logTimer = 0f;
    private const float LOG_INTERVAL = 2f; // Логируем раз в 2 секунды, чтобы не спамить

    void Awake()
    {
        bookkeepingButton = GetComponent<Button>();
        bookkeepingButton.onClick.AddListener(OnButtonClick);
    }

    void Start()
    {
        bookkeepingPanel = FindFirstObjectByType<BookkeepingPanelUI>(FindObjectsInactive.Include);
        UpdateButtonState(true); // Первичная проверка при старте
    }

    void Update()
    {
        // ----- НАЧАЛО ИЗМЕНЕНИЙ: ЛОГИРОВАНИЕ С ИНТЕРВАЛОМ -----
        logTimer += Time.deltaTime;
        if (logTimer >= LOG_INTERVAL)
        {
            logTimer = 0f;
            UpdateButtonState(true); // Вызываем проверку с логированием
        }
        else
        {
            UpdateButtonState(false); // Обычная проверка без логирования
        }
        // ----- КОНЕЦ ИЗМЕНЕНИЙ -----
    }
    
    void UpdateButtonState(bool withLog)
    {
        if (HiringManager.Instance == null) return;
        
        bool isUnlocked = false;
        StringBuilder logBuilder = withLog ? new StringBuilder() : null;
        if(withLog) logBuilder.AppendLine("<b><color=orange>--- Проверка кнопки Бухгалтерии ---</color></b>");

        foreach(var staff in HiringManager.Instance.AllStaff)
        {
            if (staff == null) continue;

            bool staffHasAction = staff.activeActions.Any(action => action.actionType == ActionType.DoBookkeeping);
            if (withLog)
            {
                var actionNames = staff.activeActions.Select(a => a.actionType.ToString());
                logBuilder.AppendLine($"  - Сотрудник: {staff.characterName} | Роль: {staff.currentRole} | Имеет 'DoBookkeeping': <color={(staffHasAction ? "green" : "red")}>{staffHasAction}</color> | Список действий: [{string.Join(", ", actionNames)}]");
            }
            
            if (staffHasAction)
            {
                isUnlocked = true;
            }
        }
        
        if (withLog)
        {
            logBuilder.AppendLine($"<b>ИТОГ: Кнопка должна быть активна: <color={(isUnlocked ? "green" : "red")}>{isUnlocked}</color></b>");
            Debug.Log(logBuilder.ToString());
        }

        if (gameObject.activeSelf != isUnlocked)
        {
            gameObject.SetActive(isUnlocked);
        }
    }

    private void OnButtonClick()
    {
        if (bookkeepingPanel != null)
        {
            bookkeepingPanel.Show();
        }
    }
}