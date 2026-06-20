// Файл: Assets/Scripts/Characters/Controllers/ClerkController.cs - ФИНАЛЬНАЯ ВЕРСИЯ
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers;
using Utilities;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(StackHolder))]
public class ClerkController : StaffController, IServiceProvider
{
    public enum ClerkState
    {
        Working,
        GoingToBreak,
        OnBreak,
        ReturningToWork,
        GoingToToilet,
        AtToilet,
        Inactive,
        StressedOut,
        GoingToArchive,
        AtArchive,
        WaitingForArchive,
        ChairPatrol
    }

    public enum ClerkRole
    {
        Regular,
        Cashier,
        Registrar,
        Archivist,
		Accountant
    }

    [Header("Настройки клерка")]
    public ClerkRole clerkRole = ClerkRole.Regular;
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

    protected override void SetArrivalState(string stateName)
    {
        if (System.Enum.TryParse<ClerkState>(stateName, out ClerkState newState))
        {
            SetState(newState);
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

    // --- НОВЫЙ МЕТОД: Правильное заступление на пост ---
    protected override IEnumerator GoToWorkstationRoutine()
    {
        // 1. Идем к столу (используем базовую логику)
        yield return base.GoToWorkstationRoutine();
        
        // 2. Когда дошли — "включаемся" в работу
        if (assignedWorkstation != null)
        {
            // --- ПРИНУДИТЕЛЬНАЯ ИНЪЕКЦИЯ НАВЫКОВ ДЛЯ REGISTRAR ---
            // Обеспечиваем минимальные навыки для корректной работы системы обслуживания
            if (this.clerkRole == ClerkRole.Registrar)
            {
                if (skills.paperworkMastery < 0.5f) skills.paperworkMastery = 0.5f;
                if (skills.softSkills < 0.5f) skills.softSkills = 0.5f;
                Debug.Log($"[ClerkController] {characterName} (Registrar): принудительно установлены навыки paperwork={skills.paperworkMastery}, softSkills={skills.softSkills}");
            }
            
            ClientSpawner.AssignServiceProviderToDesk(this, assignedWorkstation.deskId);
            SetState(ClerkState.Working);
            Debug.Log($"[ClerkController] {characterName} занял стол {assignedWorkstation.name} и готов принимать клиентов!");
        }
    }

    #region IServiceProvider Implementation
    public bool IsAvailableToServe => !IsOnBreak() && (currentState == ClerkState.Working || currentState == ClerkState.ChairPatrol);
    public Transform GetClientStandPoint() => assignedWorkstation != null ? assignedWorkstation.clientStandPoint.transform : transform;
    public ServicePoint GetWorkstation() => assignedWorkstation;

    public void AssignClient(ClientPathfinding client)
    {
        // Логика обслуживания клиентов перенесена в новые action-executor'ы
        // (ServiceAtCashierExecutor, ServiceAtRegistrationExecutor, DoBookkeepingExecutor).
        // AssignClient оставлен как no-op для совместимости с IServiceProvider.
    }
    #endregion
}