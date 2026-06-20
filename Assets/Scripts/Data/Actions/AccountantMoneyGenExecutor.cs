// Файл: Assets/Scripts/Data/Actions/AccountantMoneyGenExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Gameplay;

public class AccountantMoneyGenExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        // Получаем конфиг
        var config = Resources.Load<AIBalanceConfig>("Databases/AIBalanceConfig");
        if (config == null)
        {
            Debug.LogWarning("[AccountantMoneyGen] AIBalanceConfig не найден — действие прервано.");
            FinishAction(false);
            yield break;
        }

        // Per-role override (allRoleData живёт на ClerkController)
        var clerkForData = staff as ClerkController;
        var roleData = clerkForData?.allRoleData?.FirstOrDefault(d => d != null && d.roleType == staff.currentRole);
        float interval = config.accountantMoneyGenInterval;
        if (roleData != null && roleData.accountant_moneyGenIntervalOverride > 0f)
            interval = roleData.accountant_moneyGenIntervalOverride;

        // Стартовое сообщение
        staff.thoughtBubble?.ShowPriorityMessage("Ищу заначки...", 2f, Color.yellow);

        // Цикл пассивной генерации
        while (true)
        {
            yield return new WaitForSeconds(interval);

            // Перепроверяем состояние (вдруг бухгалтер ушёл со стола)
            if (clerkForData != null)
            {
                if (clerkForData.IsOnBreak() || clerkForData.GetCurrentState() != ClerkController.ClerkState.Working)
                    continue;
            }

            // Шанс: base + (опционально) per-role override
            float baseChance = config.accountantMoneyGenBaseChance;
            if (roleData != null && roleData.accountant_moneyGenChanceOverride >= 0f)
                baseChance = roleData.accountant_moneyGenChanceOverride;

            // dirtyHands повышает шанс (знает где искать)
            float chance = Mathf.Clamp01(baseChance + staff.skills.dirtyHands * 0.4f);
            if (Random.value > chance) continue;

            int amount = Mathf.RoundToInt(Random.Range(config.accountantMoneyGenMin, config.accountantMoneyGenMax + 1)
                                          * (1f + staff.skills.dirtyHands * 0.5f));
            amount = Mathf.Max(1, amount);

            // Теневая находка (увеличивает коррупцию, но бухгалтер её потом покрывает)
            PlayerWallet.Instance?.AddMoney(amount, $"Находка бухгалтера ({staff.name})", IncomeType.Shadow);

            staff.thoughtBubble?.ShowPriorityMessage($"Нашёл {amount}₽", 2f, Color.cyan);
        }
    }
}
