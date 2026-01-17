// Assets/Scripts/Characters/Controllers/GuardMovement.cs
using UnityEngine;
using System.Collections;
using Managers;

public class GuardMovement : MonoBehaviour
{
    public enum GuardState
    {
        Idle,
        Patrolling,
        Chasing,
        ChasingThief,
        Investigating,
        ReturningToPost,
        AtPost,
        OnPost,
        OperatingBarrier,
        WritingReport,
        Talking,
        EscortingThief,
        OnBreak,
        GoingToBreak,
        GoingToToilet,
        AtToilet,
        OffDuty,
        WaitingAtWaypoint
    }

    [Header("Состояние")]
    public GuardState currentState = GuardState.Idle;
    
    public int unwrittenReportPoints = 0;
    public float chaseSpeedMultiplier = 1.5f;
    public float talkTime = 5f;
    public float minWaitTime = 2f;
    public float maxWaitTime = 5f;
    
    // ИСПРАВЛЕНИЕ: Light2D требует правильного namespace
    public UnityEngine.Rendering.Universal.Light2D nightLight; 

    private StaffController staff;

    private void Awake()
    {
        staff = GetComponent<StaffController>();
    }

    // --- МОСТЫ ---

    public string characterName => staff != null ? staff.characterName : "Guard";
    public AgentMover AgentMover => staff.agentMover;
    public ThoughtBubbleController thoughtBubble => staff.thoughtBubble;
    public bool IsOnBreak() => staff.IsOnBreak();
    public bool IsOnDuty() => !staff.IsOnBreak();
    public void ChangeEnergy(float amount) => staff.ChangeEnergy(amount);
    public void ChangeStress(float amount) => staff.ChangeStress(amount);
    
    // --- ИСПРАВЛЕНИЕ: Добавлен недостающий метод ---
    public GuardState GetCurrentState() => currentState;
    // -----------------------------------------------

    public IEnumerator MoveToTarget(Vector3 pos, GuardState state)
    {
        SetState(state);
        return staff.MoveToTarget(pos, state.ToString());
    }

    public IEnumerator MoveToTarget(Vector3 pos, string state) => staff.MoveToTarget(pos, state);

    public string GetStatusInfo() => currentState.ToString();
    public GuardState GetCurrentStateEnum() => currentState;
    public float GetCurrentFrustration() => staff.frustration;

    public void InitializeFromData(RoleData data) { }

    public void SetState(GuardState newState)
    {
        currentState = newState;
    }

    public IEnumerator PatrolRoutine()
    {
        currentState = GuardState.Patrolling;
        var points = ScenePointsRegistry.Instance?.guardPatrolPoints;
        
        if (points == null || points.Count == 0) 
        {
            currentState = GuardState.Idle;
            yield break;
        }

        int index = 0;
        while (true)
        {
            var target = points[index];
            if (target != null)
            {
                yield return StartCoroutine(MoveToTarget(target.transform.position, GuardState.Patrolling));
                yield return new WaitForSeconds(2f);
            }
            index = (index + 1) % points.Count;
            yield return null;
        }
    }
    
    public void SelectNewPatrolPoint() { }
}