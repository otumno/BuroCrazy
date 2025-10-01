// Файл: Assets/Scripts/Characters/Controllers/InternController.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AgentMover))]
[RequireComponent(typeof(CharacterStateLogger))]
[RequireComponent(typeof(StackHolder))]
public class InternController : StaffController
{
    public enum InternState { Patrolling, HelpingConfused, ServingFromQueue, CoveringDesk, GoingToBreak, OnBreak, GoingToToilet, AtToilet, ReturningToPatrol, Inactive, Working, TalkingToConfused, TakingStackToArchive }
    
    [Header("Настройки стажера")]
    private InternState currentState = InternState.Inactive;
    private StackHolder stackHolder;

    // --- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ ИСПОЛНИТЕЛЕЙ ---
    public void SetState(InternState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger?.LogState(GetStatusInfo());
        if(visuals != null)
        {
            visuals.SetEmotionForState(newState);
        }
    }
    
	public InternState GetCurrentState()
{
    return currentState;
}
	
    public IEnumerator MoveToTarget(Vector2 targetPosition, InternState stateOnArrival)
    {
        agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        SetState(stateOnArrival);
    }

public string GetCurrentStateName()
{
    // currentState - это уникальная для каждого контроллера переменная состояния (enum)
    return currentState.ToString();
}

    // --- РЕАЛИЗАЦИЯ БАЗОВЫХ МЕТОДОВ ---
    public override bool IsOnBreak()
    {
        return currentState == InternState.OnBreak ||
               currentState == InternState.GoingToBreak ||
               currentState == InternState.AtToilet ||
               currentState == InternState.GoingToToilet;
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
    }
}