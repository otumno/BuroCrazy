// Файл: Assets/Scripts/Characters/Controllers/ClerkController.cs - ФИНАЛЬНАЯ ВЕРСИЯ
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(StackHolder))]
public class ClerkController : StaffController, IServiceProvider
{
    public enum ClerkState { Working, GoingToBreak, OnBreak, ReturningToWork, GoingToToilet, AtToilet, Inactive, StressedOut, GoingToArchive, AtArchive, WaitingForArchive, ChairPatrol }
    public enum ClerkRole { Regular, Cashier, Registrar, Archivist }

    [Header("Настройки клерка")]
    public ClerkRole role = ClerkRole.Regular;
    public List<RoleData> allRoleData;
    
    public float redirectionBonus = 0f;
    public bool IsDoingBooks = false;
    private ClerkState currentState = ClerkState.Inactive;

    public ClerkState GetCurrentState() => currentState;
    
    public void SetState(ClerkState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger?.LogState(GetStatusInfo());
        if(visuals != null)
        {
            visuals.SetEmotionForState(newState);
        }
    }

    // ----- НАЧАЛО ИЗМЕНЕНИЙ: ПРАВИЛЬНОЕ ПЕРЕОПРЕДЕЛЕНИЕ -----
    public override IEnumerator MoveToTarget(Vector2 targetPosition, string stateOnArrival)
    {
        // Преобразуем строку обратно в enum ClerkState
        if (System.Enum.TryParse<ClerkState>(stateOnArrival, out ClerkState newState))
        {
            agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
            yield return new WaitUntil(() => !agentMover.IsMoving());
            SetState(newState);
        }
        else // Если состояние не распознано, используем базовую логику
        {
             yield return base.MoveToTarget(targetPosition, stateOnArrival);
        }
    }
    // ----- КОНЕЦ ИЗМЕНЕНИЙ -----

    public void ServiceComplete()
    {
        frustration += 0.05f; 
        if (assignedWorkstation != null && assignedWorkstation.documentStack != null)
        {
            assignedWorkstation.documentStack.AddDocumentToStack();
        }
    }

    public override bool IsOnBreak()
    {
        return currentState == ClerkState.OnBreak ||
               currentState == ClerkState.GoingToBreak || 
               currentState == ClerkState.AtToilet || 
               currentState == ClerkState.GoingToToilet ||
               currentState == ClerkState.StressedOut;
    }

    public override string GetStatusInfo() => currentState.ToString();
    public override string GetCurrentStateName() => currentState.ToString();
    public void InitializeFromData(RoleData data) { /* ... */ }

    #region IServiceProvider Implementation
    public bool IsAvailableToServe => !IsOnBreak() && (currentState == ClerkState.Working || currentState == ClerkState.ChairPatrol);
    public Transform GetClientStandPoint() => assignedWorkstation != null ? assignedWorkstation.clientStandPoint.transform : transform;
    public ServicePoint GetWorkstation() => assignedWorkstation;

    public void AssignClient(ClientPathfinding client)
    {
        if (this.role == ClerkRole.Cashier)
        {
            StartCoroutine(CashierServiceRoutine(client));
        }
    }
    #endregion

    private IEnumerator CashierServiceRoutine(ClientPathfinding client)
    {
        // ... (код этого метода без изменений)
        SetState(ClerkState.Working);
        thoughtBubble?.ShowPriorityMessage($"К оплате: ${client.billToPay}", 3f, Color.white);
        yield return new WaitForSeconds(Random.Range(2f, 4f));

        int bill = client.billToPay;
        int totalSkimAmount = 0;
        RoleData roleData = allRoleData.FirstOrDefault(d => d.roleType == currentRole);
        float corruptionChanceMult = 1.0f;
        float maxSkimAmount = 0.3f;
        if (roleData != null)
        {
            corruptionChanceMult = roleData.cashier_corruptionChanceMultiplier;
            maxSkimAmount = roleData.cashier_maxSkimAmount;
        }

        float corruptionChance = (skills.corruption * 0.5f) * corruptionChanceMult;
        if (Random.value < corruptionChance && bill > 0)
        {
            totalSkimAmount = (int)(bill * Random.Range(0.1f, maxSkimAmount));
            thoughtBubble?.ShowPriorityMessage("Никто и не заметит...", 2f, new Color(0.8f, 0, 0.8f));
            yield return new WaitForSeconds(2f);
        }

        int officialAmount = bill - totalSkimAmount;
        if (officialAmount > 0)
        {
            PlayerWallet.Instance?.AddMoney(officialAmount, $"Оплата услуги (Клиент: {client.name})", IncomeType.Official);
        }

        int playerSkimCut = totalSkimAmount / 2;
        if (playerSkimCut > 0)
        {
            PlayerWallet.Instance?.AddMoney(playerSkimCut, $"Доля от махинации ({name})", IncomeType.Shadow);
        }
        
        if (client.paymentSound != null) AudioSource.PlayClipAtPoint(client.paymentSound, transform.position);
        client.billToPay = 0;
        client.isLeavingSuccessfully = true;
        client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
        client.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
        client.stateMachine.SetState(ClientState.Leaving);
        ServiceComplete();
    }
}