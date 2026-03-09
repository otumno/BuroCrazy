// Assets/Scripts/Managers/PlayerWallet.cs
using TMPro;
using UnityEngine;

namespace Managers
{
    public class PlayerWallet : MonoBehaviour
    {
        public static PlayerWallet Instance { get; private set; }

        [Header("Настройки")]
        [Tooltip("Базовый процент от официального дохода (0.1 = 10%)")]
        [Range(0f, 1f)]
        public float baseIncomeRate = 0.1f;

        // Текущий бонус от бухгалтера
        private float accountantBonus = 0f;

        // Свойство, которое возвращает итоговый процент
        public float officialIncomeRate => Mathf.Clamp01(baseIncomeRate + accountantBonus);

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
            
            // --- ВРЕМЕННЫЙ ХАК ДЛЯ ТЕСТОВ ---
            AddMoney(10000, "DEBUG: Выдача тестовых денег", IncomeType.Official);
            // --------------------------------
        }
        
        // Метод для установки бонуса (вызывается Бухгалтером)
        public void SetAccountantBonus(float bonus)
        {
            accountantBonus = bonus;
            // Можно обновить UI, если там отображается процент
        }

        public void AddMoney(int amount, string description, IncomeType type = IncomeType.Official)
        {
            int amountToWallet = 0;

            if (amount > 0) 
            {
                if (type == IncomeType.Official)
                {
                    // Используем динамическое свойство officialIncomeRate
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
            else // Расходы
            {
                amountToWallet = amount;
                currentMoney += amountToWallet;
                FinancialLedgerManager.Instance?.LogTransaction(description, amount, IncomeType.Official); 
            }

            UpdateMoneyText();
        }

        // ... (остальные методы: GetCurrentMoney, SetMoney, ResetState без изменений) ...
        public int GetCurrentMoney() { return currentMoney; }
        public bool CanAfford(int amount) => currentMoney >= amount;
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
}