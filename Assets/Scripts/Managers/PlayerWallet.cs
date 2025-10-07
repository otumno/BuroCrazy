using UnityEngine;
using TMPro;

public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; private set; }

    [Header("Настройки")]
    [Tooltip("Процент от официального дохода, который идет игроку (0.1 = 10%)")]
    [Range(0f, 1f)]
    public float officialIncomeRate = 0.1f;

    [Header("UI Компоненты")]
    public TextMeshProUGUI moneyText;
    public GameObject moneyEffectPrefab;

    private int currentMoney = 0;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); } else { Instance = this; }
    }

    void Start()
    {
        UpdateMoneyText();
    }

    /// <summary>
    /// Универсальный метод для изменения количества денег.
    /// </summary>
    /// <param name="amount">Сумма. Положительная для дохода, отрицательная для расхода.</param>
    /// <param name="description">Описание для лога.</param>
    /// <param name="type">Тип дохода (для положительных сумм).</param>
    public void AddMoney(int amount, string description, IncomeType type = IncomeType.Official)
    {
        int amountToWallet = 0;

        if (amount > 0) // --- ЛОГИКА ДОХОДОВ ---
        {
            if (type == IncomeType.Official)
            {
                amountToWallet = (int)(amount * officialIncomeRate);
                currentMoney += amountToWallet;
                FinancialLedgerManager.Instance?.LogTransaction($"{description} (Вам {officialIncomeRate:P0})", amount, type);
            }
            else // Shadow
            {
                amountToWallet = amount;
                currentMoney += amountToWallet;
                FinancialLedgerManager.Instance?.LogTransaction(description, amount, type);
            }
        }
        else // --- ЛОГИКА РАСХОДОВ ---
        {
            amountToWallet = amount;
            currentMoney += amountToWallet;
            FinancialLedgerManager.Instance?.LogTransaction(description, amount, IncomeType.Official); // Расходы всегда "официальные"
        }

        UpdateMoneyText();
    }

    // Старые методы для совместимости (можно будет потом удалить)
    public void AddMoney(int amount, Vector3 spawnPosition) { AddMoney(amount, "Неизвестная операция"); }

    public int GetCurrentMoney() { return currentMoney; }
    public void SetMoney(int amount) { currentMoney = amount; UpdateMoneyText(); }
    public void ResetState(int startingMoney = 100) { currentMoney = startingMoney; UpdateMoneyText(); }

    private void UpdateMoneyText()
    {
        if (moneyText != null)
        {
            moneyText.text = $"Счет: ${currentMoney}";
        }
    }
}