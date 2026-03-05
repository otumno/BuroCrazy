// Assets/Scripts/Characters/Controllers/GuardMovement.cs
using UnityEngine;
using System.Collections;
using Managers;
using Utilities;

[RequireComponent(typeof(AgentMover), typeof(CharacterStateLogger))]
public class GuardMovement : StaffController
{
    public enum GuardState
    {
        Idle, Patrolling, Chasing, ChasingThief, Investigating, ReturningToPost, AtPost, OnPost,
        OperatingBarrier, WritingReport, Talking, EscortingThief, OnBreak, GoingToBreak,
        GoingToToilet, AtToilet, OffDuty, WaitingAtWaypoint
    }

    [Header("Состояние")]
    public GuardState currentState = GuardState.Idle;
    
    public int unwrittenReportPoints = 0;
    public float chaseSpeedMultiplier = 1.5f;
    public float talkTime = 5f;
    public float minWaitTime = 2f;
    public float maxWaitTime = 5f;
    
    public UnityEngine.Rendering.Universal.Light2D nightLight; 

    protected override void Awake()
    {
        base.Awake();
        var references = GetComponent<StaffPrefabReferences>();
        if (references != null && references.nightLight != null)
        {
            nightLight = references.nightLight.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
        }
    }

    public GuardState GetCurrentState() => currentState;
    public GuardState GetCurrentStateEnum() => currentState;

    public void SetState(GuardState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        logger?.LogState(GetStatusInfo());
        if (visuals != null) visuals.SetEmotionForState(newState);
    }

    public override string GetStatusInfo() => currentState.ToString();
    public override string GetCurrentStateName() => currentState.ToString();
    public override float GetCurrentFrustration() => frustration;

    public override bool IsOnBreak()
    {
        return currentState == GuardState.OnBreak ||
               currentState == GuardState.GoingToBreak ||
               currentState == GuardState.AtToilet ||
               currentState == GuardState.GoingToToilet ||
               currentState == GuardState.OffDuty;
    }

    public override void InitializeFromData(RoleData data)
    {
        base.InitializeFromData(data);
        if (data != null)
        {
            chaseSpeedMultiplier = data.guard_chaseSpeedMultiplier;
            talkTime = data.guard_talkTime;
            minWaitTime = data.guard_minWaitTime;
            maxWaitTime = data.guard_maxWaitTime;
        }
    }

    public IEnumerator MoveToTarget(Vector3 pos, GuardState stateOnArrival)
    {
        if (agentMover != null)
        {
            agentMover.SetPath(PathfindingUtility.BuildPathTo(transform.position, pos, gameObject));
            yield return new WaitUntil(() => agentMover == null || !agentMover.IsMoving());
        }
        SetState(stateOnArrival);
    }

    public override IEnumerator MoveToTarget(Vector2 targetPosition, string stateOnArrival)
    {
        if (System.Enum.TryParse<GuardState>(stateOnArrival, out GuardState newState))
        {
            yield return StartCoroutine(MoveToTarget((Vector3)targetPosition, newState));
        }
        else
        {
            yield return base.MoveToTarget(targetPosition, stateOnArrival);
        }
    }

    protected override void SetArrivalState(string stateName)
    {
        if (System.Enum.TryParse<GuardState>(stateName, out GuardState newState))
        {
            SetState(newState);
        }
        else
        {
            base.SetArrivalState(stateName);
        }
    }

    public IEnumerator PatrolRoutine()
    {
        currentState = GuardState.Patrolling;
        var points = ScenePointsRegistry.Instance?.guardPatrolPoints;
        
        if (points == null || points.Count == 0) 
        {
            Debug.LogWarning("[GuardMovement] Patrol points не найдены или пусты");
            SetState(GuardState.Idle);
            yield break;
        }

        int index = 0;
        while (IsOnDuty())
        {
            if (index >= points.Count) index = 0;
            
            var target = points[index];
            if (target != null)
            {
                yield return StartCoroutine(MoveToTarget(target.transform.position, GuardState.Patrolling));
                yield return new WaitForSeconds(2f);
            }
            index++;
            yield return null;
        }
    }
    
    public void SelectNewPatrolPoint() { }
}
