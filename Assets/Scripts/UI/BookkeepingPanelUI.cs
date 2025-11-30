using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Text;
using Managers;

public class BookkeepingPanelUI : MonoBehaviour
{
    [Header("Ссылки на UI элементы")]
    [SerializeField] private Button closeButton; // Ваша кнопка "Назад"

    [Header("Поля статистики")]
    [SerializeField] private TextMeshProUGUI officialGrossText;
    [SerializeField] private TextMeshProUGUI playersCutText;
    [SerializeField] private TextMeshProUGUI shadowIncomeText;
    [SerializeField] private TextMeshProUGUI totalBalanceText;
    [SerializeField] private TextMeshProUGUI salaryFundText;
    [SerializeField] private TextMeshProUGUI corruptionLevelText;
    [SerializeField] private TextMeshProUGUI dailyExpensesText;
    [SerializeField] private Transform transactionLogContent;

    [Header("Префабы и настройки")]
    [SerializeField] private GameObject transactionLogEntryPrefab; // Префаб для одной строки лога

    private void Awake()
    {
        // Назначаем действие для кнопки "Назад"
        closeButton.onClick.AddListener(Hide);
    }

    private void OnEnable()
    {
        // При каждом открытии панели обновляем всю информацию
        UpdateAllData();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        Time.timeScale = 0f; // Ставим игру на паузу, когда открыта бухгалтерия
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f; // Снимаем игру с паузы при закрытии
    }

    // Главный метод для обновления всех данных на панели
public void UpdateAllData()
{
    var ledger = FinancialLedgerManager.Instance;
    var wallet = PlayerWallet.Instance;
    var staffList = HiringManager.Instance.AllStaff;

    if (ledger == null || wallet == null || staffList == null || CalendarManager.Instance == null)
    {
        Debug.LogError("Один из менеджеров (Ledger, Wallet, Hiring, Calendar) не найден!");
        return;
    }

    // --- ИСПРАВЛЕНО: Берем день из CalendarManager ---
    int currentDay = CalendarManager.Instance.CurrentDay; 

    // 1. Считаем доходы из лога за текущий день
    int officialGross = ledger.dailyLog
        .Where(t => t.day == currentDay && t.type == IncomeType.Official && t.amount > 0)
        .Sum(t => t.amount);

    int shadowIncome = ledger.dailyLog
        .Where(t => t.day == currentDay && t.type == IncomeType.Shadow)
        .Sum(t => t.amount);

    // 2. Считаем зарплатный фонд
    int estimatedPayroll = 0;
    foreach(var staff in staffList)
    {
        if(staff != null) estimatedPayroll += staff.unpaidPeriods * staff.salaryPerPeriod;
    }

    // 3. Считаем расходы за день
    int dailyExpenses = ledger.dailyLog
        .Where(t => t.day == currentDay && t.amount < 0)
        .Sum(t => t.amount);

    // ... (дальше код обновления UI текстов без изменений)
    officialGrossText.text = $"Подотчетный доход: ${officialGross}";
    playersCutText.text = $"Ваша доля ({wallet.officialIncomeRate:P0}): ${Mathf.RoundToInt(officialGross * wallet.officialIncomeRate)}";
    shadowIncomeText.text = $"Теневой доход: ${shadowIncome}";
    totalBalanceText.text = $"Остаток на счете: ${wallet.GetCurrentMoney()}";
    salaryFundText.text = $"Зарплатный фонд (к выплате): ${estimatedPayroll}";
    corruptionLevelText.text = $"Уровень коррупции: {ledger.globalCorruptionScore}";
    dailyExpensesText.text = $"Расходы сегодня: ${Mathf.Abs(dailyExpenses)}";

    // --- Обновляем лог транзакций ---
    foreach (Transform child in transactionLogContent) Destroy(child.gameObject);

    if (transactionLogEntryPrefab != null)
    {
        // Берем последние 15 транзакций за СЕГОДНЯ
        var todaysTransactions = ledger.dailyLog
            .Where(t => t.day == currentDay)
            .Reverse()
            .Take(15);

        foreach (var transaction in todaysTransactions)
        {
            GameObject entryGO = Instantiate(transactionLogEntryPrefab, transactionLogContent);
            var entryText = entryGO.GetComponent<TextMeshProUGUI>();
            if (entryText != null)
            {
                string sign = transaction.amount >= 0 ? "+" : "";
                Color color = Color.white;
                if (transaction.amount < 0) color = Color.red;
                else if (transaction.type == IncomeType.Shadow) color = new Color(0.8f, 0.4f, 1f);

                entryText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{transaction.description}: {sign}${transaction.amount}</color>";
            }
        }
    }
}
}