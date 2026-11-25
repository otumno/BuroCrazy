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
            // Тестовые деньги здесь не выдаём: при старте сессии GameSession сбрасывает кошелёк и загружает сейв,
            // и начисление отсюда затиралось. Их выдаёт BuroDebug.DebugMoney из GameSession, после загрузки сейва.
            UpdateMoneyText();
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
                    FinancialLedgerManager.Instance?.LogTransaction(UI.GameText.Get("ledger.player_share", description, officialIncomeRate), amount, type);
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

#if DEBUG_ENABLED
        /// <summary>
        /// Тестовые деньги: зачисляются целиком, в обход доли с официального дохода.
        /// </summary>
        // Не через AddMoney: официальный доход урезается до officialIncomeRate (10%), а теневой поднимает коррупцию
        // и бьёт по репутации. В журнал бухгалтерии тоже не пишем: запись исказила бы итоги дня.
        public void AddDebugMoney(int amount, string reason)
        {
            currentMoney += amount;
            UpdateMoneyText();
            Debug.Log($"[PlayerWallet] DEBUG: +{amount} ({reason}). Баланс: {currentMoney}");
        }
#endif

        // ... (остальные методы: GetCurrentMoney, SetMoney, ResetState без изменений) ...
        public int GetCurrentMoney() { return currentMoney; }
        public bool CanAfford(int amount) => currentMoney >= amount;
        public void SetMoney(int amount) { currentMoney = amount; UpdateMoneyText(); }
        public void ResetState(int startingMoney = 100) { currentMoney = startingMoney; UpdateMoneyText(); }

        private void UpdateMoneyText()
        {
            if (moneyText != null)
            {
                var localized = moneyText.GetComponent<UI.LocalizedText>() ?? moneyText.gameObject.AddComponent<UI.LocalizedText>();
                localized.SetReference(UI.GameText.Table, "wallet.balance", currentMoney);
            }
        }
    }
}
