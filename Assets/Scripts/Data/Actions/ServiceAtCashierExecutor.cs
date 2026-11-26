// Assets/Scripts/Data/Actions/ServiceAtCashierExecutor.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using Managers;
using Gameplay; // Подключаем пространство имен с OfficeObjectDurability
using Clinch;
using Enums;

public class ServiceAtCashierExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        yield return ReturnToWorkstation();
        if (!IsAtWorkstation) { FinishAction(false); yield break; }
        var cashier = staff as ClerkController;
        if (cashier == null || cashier.assignedWorkstation == null)
        {
            FinishAction(false);
            yield break;
        }

        var zone = cashier.assignedWorkstation.GetComponentInParent<LimitedCapacityZone>();
        var client = zone?.GetOccupyingClients().FirstOrDefault(c => CanServeAtWorkstation(c) && (c.billToPay > 0 || c.mainGoal == ClientGoal.PayTax));
        
        // Бронебойный поиск физически ближайшего клиента у кассы
        if (client == null && cashier.assignedWorkstation != null)
        {
            client = Object.FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None)
                .FirstOrDefault(c => CanServeAtWorkstation(c) && (c.billToPay > 0 || c.mainGoal == ClientGoal.PayTax));
        }

        if (!TryBeginClientService(client))
        {
            FinishAction(false);
            yield break;
        }

        // --- КЛИНЧ-ПРОВЕРКА (НЕЗАВИСИМЫЕ КУБИКИ) ---
        var clinchConfig = Gameplay.AIBalanceConfig.Instance;
        Clinch.ClinchTarget clinchToTrigger = null;
        
        if (clinchConfig != null)
        {
            // Бросок для клиента
            bool rollClient = Random.value < (clinchConfig.clientClinchBaseChance * (1f + client.suetunFactor));
            // Бросок для персонала
            bool rollStaff = Random.value < (clinchConfig.staffClinchBaseChance * (1f - staff.skills.sedentaryResilience));
            
            Clinch.ClinchTarget clinch = null;
            
            if (rollClient)
            {
                clinch = client.GetComponent<Clinch.ClinchTarget>();
                if (clinch == null) clinch = client.gameObject.AddComponent<Clinch.ClinchTarget>();
            }
            else if (rollStaff)
            {
                clinch = staff.GetComponent<Clinch.ClinchTarget>();
                if (clinch == null) clinch = staff.gameObject.AddComponent<Clinch.ClinchTarget>();
            }

            if (clinch != null && !clinch.IsActive)
            {
                clinchToTrigger = clinch;
            }
            
            if (clinchToTrigger != null)
            {
                clinchToTrigger.TriggerClinch();
                staff.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.service.call_director"), 2f, Color.yellow);
                yield return new WaitWhile(() => clinchToTrigger != null && clinchToTrigger.IsActive && CanServeAtWorkstation(client));
                yield return WaitForClientService(client, 2.5f); // Даём реакции проявиться
            }
        }
        // После WaitWhile ОБЯЗАТЕЛЬНАЯ ПРОВЕРКА для всех экзекуторов:
        if (!CanServeAtWorkstation(client))
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
            cashier.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.cashier.broken"), 3f, Color.red);
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

        cashier.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.cashier.payment_due", client.billToPay), 3f, Color.white);
        
        // --- ПРИМЕНЕНИЕ ЭФФЕКТИВНОСТИ ---
        // Ожидание зависит от состояния кассы
        float processingTime = Random.Range(2f, 4f) / Mathf.Max(.05f, efficiency);
        ShowProcessing(client.transform.position, processingTime);
        yield return WaitForClientService(client, processingTime);
        if (!CanServeAtWorkstation(client)) { FinishAction(false); yield break; }

        // --- НАНЕСЕНИЕ УРОНА КАССЕ ---
        if (durability != null)
        {
            // Касса ломается чуть медленнее, чем обычные столы
            durability.Degrade(Random.Range(1f, 3f)); 
        }
        // -----------------------------

        int bill = client.billToPay;
        int totalSkimAmount = 0;
        
        RoleData roleData = cashier.roleData ?? cashier.allRoleData?.FirstOrDefault(d => d != null && d.roleType == cashier.currentRole);
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
            cashier.thoughtBubble?.ShowPriorityMessage(UI.GameText.Get("staff.cashier.skim"), 2f, new Color(0.8f, 0, 0.8f));
            yield return WaitForClientService(client, 2f);
        }

        if (!CanServeAtWorkstation(client) || client.billToPay != bill || bill <= 0 || PlayerWallet.Instance == null)
        {
            Debug.LogWarning("[ClientServiceTrace] Payment postponed: client/bill/wallet changed.");
            FinishAction(false);
            yield break;
        }
        int walletBefore = PlayerWallet.Instance.GetCurrentMoney();
        int officialAmount = bill - totalSkimAmount;
        if (officialAmount > 0)
        {
            PlayerWallet.Instance?.AddMoney(officialAmount, UI.GameText.Get("ledger.service_payment", client.name), IncomeType.Official);
        }
        
        int playerSkimCut = totalSkimAmount / 2;
        if (playerSkimCut > 0)
        {
            PlayerWallet.Instance?.AddMoney(playerSkimCut, UI.GameText.Get("ledger.skim_share", cashier.name), IncomeType.Shadow);
        }

        if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, staff.transform.position);
        client.billToPay = 0;
        Debug.Log($"[ClientServiceTrace] Оплата: клиент={client.name}, счёт={bill}, кошелёк={walletBefore}->{PlayerWallet.Instance.GetCurrentMoney()}");
        
        client.isLeavingSuccessfully = true;
        client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
        client.stateMachine.SetGoal(ClientSpawner.Instance?.exitWaypoint);
        client.stateMachine.SetState(ClientState.Leaving);
        
        cashier.ServiceComplete();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
}
