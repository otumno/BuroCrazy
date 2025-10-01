// Файл: Assets/Scripts/Characters/Controllers/ClerkController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(StackHolder))]
public class ClerkController : StaffController
{
    public enum ClerkState { Working, GoingToBreak, OnBreak, ReturningToWork, GoingToToilet, AtToilet, Inactive, StressedOut, GoingToArchive, AtArchive }
    public enum ClerkRole { Regular, Cashier, Registrar, Archivist }

    [Header("Настройки клерка")]
    public ClerkRole role = ClerkRole.Regular;
    public ServicePoint assignedServicePoint;

    public float timeInToilet;
    public float clientArrivalTimeout;
    public float maxStress;
    public float stressGainPerClient;
    public float stressReliefRate;

    private ClerkState currentState = ClerkState.Inactive;
    private StackHolder stackHolder;

    public string GetCurrentStateName()
    {
        return currentState.ToString();
    }

public ClerkState GetCurrentState()
{
    return currentState;
}

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

    public IEnumerator MoveToTarget(Vector2 targetPosition, ClerkState stateOnArrival)
    {
        agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }

    // --- ВОЗВРАЩЕНО: Важный метод для взаимодействия с клиентами ---
    public void ServiceComplete()
    {
        currentFrustration += stressGainPerClient;
        if (assignedServicePoint != null && assignedServicePoint.documentStack != null)
        {
            if (role == ClerkRole.Cashier || role == ClerkRole.Regular)
            {
                assignedServicePoint.documentStack.AddDocumentToStack();
            }
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

    public override string GetStatusInfo()
    {
        return currentState.ToString();
    }

    public void InitializeFromData(RoleData data)
    {
        if (agentMover != null)
        {
            agentMover.moveSpeed = data.moveSpeed;
            agentMover.priority = data.priority;
        }

        this.spriteCollection = data.spriteCollection;
        this.stateEmotionMap = data.stateEmotionMap;
        if(visuals != null)
        {
            visuals.EquipAccessory(data.accessoryPrefab);
        }

        this.timeInToilet = data.clerk_timeInToilet;
        this.clientArrivalTimeout = data.clerk_clientArrivalTimeout;
        this.maxStress = data.clerk_maxStress;
        this.stressGainPerClient = data.clerk_stressGainPerClient;
        this.stressReliefRate = data.clerk_stressReliefRate;
    }
}