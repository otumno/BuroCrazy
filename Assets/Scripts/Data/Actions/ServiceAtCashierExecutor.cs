// Assets/Scripts/Data/Actions/ServiceAtCashierExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Gameplay; // Подключаем пространство имен с OfficeObjectDurability
using Gameplay;
using Clinch;
using Enums;

public class ServiceAtCashierExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var cashier = staff as ClerkController;
        if (cashier == null || cashier.assignedWorkstation == null)
        {
            FinishAction(false);
            yield break;
        }

        var zone = ClientSpawner.GetZoneByDeskId(cashier.assignedWorkstation.deskId);
        var client = zone?.GetOccupyingClients().FirstOrDefault(c => c.billToPay > 0 || c.mainGoal == ClientGoal.PayTax);

        if (client == null)
        {
            FinishAction(false);
            yield break;
        }

        // --- КЛИНЧ-ПРОВЕРКА ---
        var clinchConfig = Gameplay.AIBalanceConfig.Instance;
        if (clinchConfig != null && clinchConfig.clinchBaseChance > 0f && Random.value < clinchConfig.clinchBaseChance)
        {
            Clinch.ClinchTarget clinch = null;
            
            if (client != null)
            {
                clinch = client.GetComponent<Clinch.ClinchTarget>();
                if (clinch == null) clinch = client.gameObject.AddComponent<Clinch.ClinchTarget>();
            }
            else if (staff != null)
            {
                clinch = staff.GetComponent<Clinch.ClinchTarget>();
                if (clinch == null) clinch = staff.gameObject.AddComponent<Clinch.ClinchTarget>();
            }

            if (clinch != null && !clinch.IsActive)
            {
                clinch.TriggerClinch();
                staff.thoughtBubble?.ShowPriorityMessage("Эмм... Директор!", 2f, Color.yellow);
                yield return new WaitWhile(() => clinch.IsActive);
            }
        }
        // После WaitWhile ОБЯЗАТЕЛЬНАЯ ПРОВЕРКА для всех экзекуторов:
        if (client != null && client.stateMachine != null && client.stateMachine.GetCurrentState() == ClientState.LeavingUpset)
        {
            // Клиент обиделся и ушел по таймауту клинча
            if (staff is ClerkController c) c.SetState(ClerkController.ClerkState.Working);
            FinishAction(false);
            yield break;
        }
        // --- КЛИНЧ-ПРОВЕРКА (КОНЕЦ) ---

        // --- ИНТЕГРАЦИЯ ИЗНОСА (НАЧАЛО) ---
        var durability = cashier.assignedWorkstation.GetComponent<OfficeObjectDurability>();
        if (durability != null && !durability.IsUsable())
        {
            cashier.thoughtBubble?.ShowPriorityMessage("Касса сломана!", 3f, Color.red);
            // Не забываем сбрасывать состояние, если оно было изменено
            cashier.SetState(ClerkController.ClerkState.Working);
            FinishAction(false);
            yield break;
        }
        float efficiency = (durability != null) ? durability.GetEfficiencyMultiplier() : 1.0f;
        // --- ИНТЕГРАЦИЯ ИЗНОСА (КОНЕЦ) ---

        cashier.SetState(ClerkController.ClerkState.Working);
        
        if (client.billToPay == 0 && client.mainGoal == ClientGoal.PayTax)
        {
             client.billToPay = Random.Range(20, 121);
        }

        cashier.thoughtBubble?.ShowPriorityMessage($"К оплате: ${client.billToPay}", 3f, Color.white);
        
        // --- ПРИМЕНЕНИЕ ЭФФЕКТИВНОСТИ ---
        // Ожидание зависит от состояния кассы
        yield return new WaitForSeconds(Random.Range(2f, 4f) / efficiency);

        // --- НАНЕСЕНИЕ УРОНА КАССЕ ---
        if (durability != null)
        {
            // Касса ломается чуть медленнее, чем обычные столы
            durability.Degrade(Random.Range(1f, 3f)); 
        }
        // -----------------------------

        int bill = client.billToPay;
        int totalSkimAmount = 0;
        
        RoleData roleData = cashier.allRoleData.FirstOrDefault(d => d.roleType == cashier.currentRole);
        float corruptionChanceMult = 1.0f;
        float maxSkimAmount = 0.3f;
        if (roleData != null)
        {
            corruptionChanceMult = roleData.cashier_corruptionChanceMultiplier;
            maxSkimAmount = roleData.cashier_maxSkimAmount;
        }

        float corruptionChance = (cashier.skills.corruption * 0.5f) * corruptionChanceMult;
        if (Random.value < corruptionChance && bill > 0)
        {
            totalSkimAmount = (int)(bill * Random.Range(0.1f, maxSkimAmount));
            cashier.thoughtBubble?.ShowPriorityMessage("Никто и не заметит...", 2f, new Color(0.8f, 0, 0.8f));
            yield return new WaitForSeconds(2f);
        }

        int officialAmount = bill - totalSkimAmount;
        if (officialAmount > 0)
        {
            PlayerWallet.Instance?.AddMoney(officialAmount, $"Оплата услуги ({client.name})", IncomeType.Official);
        }
        
        int playerSkimCut = totalSkimAmount / 2;
        if (playerSkimCut > 0)
        {
            PlayerWallet.Instance?.AddMoney(playerSkimCut, $"Доля от махинации ({cashier.name})", IncomeType.Shadow);
        }

        if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, staff.transform.position);
        client.billToPay = 0;
        
        client.isLeavingSuccessfully = true;
        client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
        client.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
        client.stateMachine.SetState(ClientState.Leaving);
        
        cashier.ServiceComplete();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}