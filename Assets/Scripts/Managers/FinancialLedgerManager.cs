using UnityEngine;
using System.Collections.Generic;

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
            day = ClientSpawner.Instance.GetCurrentDay()
        });

        // Если транзакция "теневая", увеличиваем уровень коррупции
        if (type == IncomeType.Shadow)
        {
            globalCorruptionScore += Mathf.Abs(amount);
            Debug.Log($"<color=purple>СЧЕТЧИК КОРРУПЦИИ:</color> Увеличен на {Mathf.Abs(amount)}. Текущее значение: {globalCorruptionScore}");
        }
    }

    public void ResetDay()
    {
        dailyLog.Clear();
        // Глобальный счетчик коррупции НЕ сбрасываем каждый день! Он накапливается.
    }
}