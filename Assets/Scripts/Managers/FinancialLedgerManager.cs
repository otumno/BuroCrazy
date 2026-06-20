using System.Collections.Generic;
using UnityEngine;

namespace Managers
{
    // Тип дохода, чтобы различать "белые" и "черные" деньги
    public enum IncomeType { Official, Shadow }

// Структура для хранения одной транзакции
    [System.Serializable]
    public class Transaction
    {
        public string description;
        public int amount;
        public IncomeType type;
        public int day;
    }

    public class FinancialLedgerManager : MonoBehaviour
    {
        public static FinancialLedgerManager Instance { get; private set; }

        public List<Transaction> dailyLog = new List<Transaction>();

        // --- НАШ НОВЫЙ СЧЕТЧИК КОРРУПЦИИ ---
        public int globalCorruptionScore = 0;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); } else { Instance = this; }
        }

        /// <summary>
        /// Записывает транзакцию в лог и обновляет счетчик коррупции.
        /// </summary>
        public void LogTransaction(string desc, int amount, IncomeType type)
        {
            if (ClientSpawner.Instance == null) return;

            dailyLog.Add(new Transaction
            {
                description = desc,
                amount = amount,
                type = type,
                day = CalendarManager.Instance.CurrentDay
            });

            // Если транзакция "теневая", увеличиваем уровень коррупции
            if (type == IncomeType.Shadow)
            {
                globalCorruptionScore += Mathf.Abs(amount);
                Debug.Log($"<color=purple>СЧЕТЧИК КОРРУПЦИИ:</color> Увеличен на {Mathf.Abs(amount)}. Текущее значение: {globalCorruptionScore}");
                
                // Наносим урон репутации за коррупцию
                DirectorManager.Instance?.RegisterCorruption(Mathf.Abs(amount));
            }
        }

        public void ResetDay()
        {
            dailyLog.Clear();
            // Глобальный счетчик коррупции НЕ сбрасываем каждый день! Он накапливается.
        }

        /// <summary>
        /// Снижает globalCorruptionScore на указанную величину (не ниже 0).
        /// Используется AccountantCoverSchemesExecutor.
        /// </summary>
        public void ReduceCorruption(int amount)
        {
            if (amount <= 0) return;
            int old = globalCorruptionScore;
            globalCorruptionScore = Mathf.Max(0, globalCorruptionScore - amount);
            int actual = old - globalCorruptionScore;
            if (actual > 0)
            {
                Debug.Log($"<color=purple>СЧЕТЧИК КОРРУПЦИИ:</color> Снижен на {actual}. Текущее значение: {globalCorruptionScore}");
            }
        }
    }
}