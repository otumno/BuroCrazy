using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D), typeof(AgentMover), typeof(CharacterStateLogger))]
public class GuardMovement : StaffController
{
    public enum GuardState { Idle, Patrolling, WaitingAtWaypoint, Chasing, Talking, OnPost, GoingToBreak, OnBreak, GoingToToilet, AtToilet, OffDuty, ChasingThief, EscortingThief, Evicting, StressedOut, WritingReport, OperatingBarrier }
    
    [Header("Состояние Охранника")]
    private GuardState currentState = GuardState.OffDuty;

    [Header("Механика Протоколов")]
    [Tooltip("Количество действий, за которые нужно отчитаться.")]
    public int unwrittenReportPoints = 0;

    [Header("Объекты (Prefab)")]
    public GameObject nightLight;

    [Header("Уникальные параметры Охранника")]
    public float minWaitTime;
    public float maxWaitTime;
    public float chaseSpeedMultiplier;
    public float talkTime;
    public float timeInToilet;
    public float maxStress;
    public float stressGainPerViolator;
    public float stressReliefRate;
    
    // Мы удалили отсюда методы Awake() и TryToStartNewAction(), так как они больше не нужны.

    public void SetState(GuardState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger?.LogState(GetStatusInfo());
        if(visuals != null && stateEmotionMap != null)
        {
            visuals.SetEmotionForState(newState);
        }
    }

    public GuardState GetCurrentState()
    {
        return currentState;
    }

    public string GetCurrentStateName()
    {
        return currentState.ToString();
    }

    public Transform SelectNewPatrolPoint()
    {
        var points = ScenePointsRegistry.Instance?.guardPatrolPoints;
        if (points == null || !points.Any()) return null;
        return points[Random.Range(0, points.Count)];
    }
    
    public IEnumerator MoveToTarget(Vector2 targetPosition, GuardState stateOnArrival)
    {
        AgentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, targetPosition, this.gameObject));
        yield return new WaitUntil(() => !AgentMover.IsMoving());
        SetState(stateOnArrival);
    }

    public override bool IsOnBreak()
    {
        return currentState == GuardState.OnBreak ||
               currentState == GuardState.GoingToBreak ||
               currentState == GuardState.AtToilet ||
               currentState == GuardState.GoingToToilet ||
               currentState == GuardState.StressedOut ||
               currentState == GuardState.OnPost;
    }

    public override string GetStatusInfo()
    {
        return currentState.ToString();
    }

    public void InitializeFromData(RoleData data)
    {
        this.minWaitTime = data.guard_minWaitTime;
        this.maxWaitTime = data.guard_maxWaitTime;
        this.chaseSpeedMultiplier = data.guard_chaseSpeedMultiplier;
        this.talkTime = data.guard_talkTime;
        this.timeInToilet = data.guard_timeInToilet;
        this.maxStress = data.guard_maxStress;
        this.stressGainPerViolator = data.guard_stressGainPerViolator;
        this.stressReliefRate = data.guard_stressReliefRate;
    }
}