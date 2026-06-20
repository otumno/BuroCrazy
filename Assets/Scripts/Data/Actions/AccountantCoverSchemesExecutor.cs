// Файл: Assets/Scripts/Data/Actions/AccountantCoverSchemesExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Gameplay;

public class AccountantCoverSchemesExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        var config = Resources.Load<AIBalanceConfig>("Databases/AIBalanceConfig");
        if (config == null)
        {
            Debug.LogWarning("[AccountantCoverSchemes] AIBalanceConfig не найден — действие прервано.");
            FinishAction(false);
            yield break;
        }

        var clerkForData = staff as ClerkController;
        var roleData = clerkForData?.allRoleData?.FirstOrDefault(d => d != null && d.roleType == staff.currentRole);
        float interval = config.accountantCoverSchemesInterval;
        if (roleData != null && roleData.accountant_coverSchemesIntervalOverride > 0f)
            interval = roleData.accountant_coverSchemesIntervalOverride;

        staff.thoughtBubble?.ShowPriorityMessage("Закрываю дыры...", 2f, Color.magenta);

        while (true)
        {
            yield return new WaitForSeconds(interval);

            if (clerkForData != null)
            {
                if (clerkForData.IsOnBreak() || clerkForData.GetCurrentState() != ClerkController.ClerkState.Working)
                    continue;
            }

            // Базовый шанс
            float baseChance = config.accountantCoverSchemesBaseChance;
            if (roleData != null && roleData.accountant_coverChanceOverride >= 0f)
                baseChance = roleData.accountant_coverChanceOverride;

            // Больше dirtyHands = лучше знает как покрыть (по решению игрока)
            float chance = Mathf.Clamp01(baseChance + staff.skills.dirtyHands * config.accountantCoverSchemesDirtyHandsBonus);
            if (Random.value > chance) continue;

            int reduce = Random.Range(config.accountantCoverSchemesReduceMin, config.accountantCoverSchemesReduceMax + 1);
            FinancialLedgerManager.Instance?.ReduceCorruption(reduce);

            staff.thoughtBubble?.ShowPriorityMessage($"Прикрыл на {reduce}₽", 2f, Color.green);
        }
    }
}
