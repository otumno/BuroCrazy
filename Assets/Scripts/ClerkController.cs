using UnityEngine;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(Rigidbody2D))]
public class ClerkController : MonoBehaviour
{
    public enum ClerkState { Working, GoingToBreak, OnBreak, ReturningToWork, GoingToToilet, AtToilet, Inactive }
    private ClerkState currentState = ClerkState.Inactive;
    [Header("Назначение клерка")]
    public Transform workPoint;
    public Transform kitchenPoint;
    public Transform staffToiletPoint;
    public int assignedServicePoint;
    [Header("Параметры поведения")]
    public float moveSpeed = 1.8f;
    public float timeInToilet = 10f;
    public float chanceToGoToToilet = 0.005f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 targetPosition;
    private Coroutine currentActionCoroutine;
    private Waypoint[] allWaypoints;
    private Waypoint currentTargetWaypoint;
    private bool isWorking = false;

    void Awake() { rb = GetComponent<Rigidbody2D>(); spriteRenderer = GetComponentInChildren<SpriteRenderer>(); allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None); }
    void Start() { ClientQueueManager.SetServicePointAvailability(assignedServicePoint, false); }
    
    void FixedUpdate() { if (!isWorking || Time.timeScale == 0f) { rb.linearVelocity = Vector2.zero; return; } if (currentState == ClerkState.Working || currentState == ClerkState.OnBreak || currentState == ClerkState.AtToilet) { rb.linearVelocity = Vector2.zero; if (currentState == ClerkState.Working && Random.value < chanceToGoToToilet * Time.deltaTime) { if (currentActionCoroutine == null) { currentActionCoroutine = StartCoroutine(ToiletBreakRoutine()); } } } else { MoveTowardsTarget(); } UpdateSpriteDirection(); }

    public void StartShift() { if(currentActionCoroutine != null) StopCoroutine(currentActionCoroutine); currentActionCoroutine = StartCoroutine(StartShiftRoutine()); }
    public void EndShift() { if(currentActionCoroutine != null) StopCoroutine(currentActionCoroutine); currentActionCoroutine = StartCoroutine(EndShiftRoutine()); }
    public void GoOnBreak(float duration) { if(currentActionCoroutine != null) StopCoroutine(currentActionCoroutine); currentActionCoroutine = StartCoroutine(BreakRoutine(duration)); }
    public ClerkState GetCurrentState() => currentState;
    public string GetStatusInfo() { switch (currentState) { case ClerkState.Working: return $"Работает: {workPoint.name}"; case ClerkState.OnBreak: return "На перерыве (кухня)"; case ClerkState.AtToilet: return "На перерыве (туалет)"; case ClerkState.ReturningToWork: return $"Возвращается на работу: {workPoint.name}"; case ClerkState.GoingToBreak: return $"Идет на перерыв: {kitchenPoint.name}"; case ClerkState.GoingToToilet: return $"Идет в туалет: {staffToiletPoint.name}"; default: return currentState.ToString(); } }

    private IEnumerator StartShiftRoutine() { yield return new WaitForSeconds(10f + Random.Range(-10f, 10f)); isWorking = true; yield return StartCoroutine(ReturnToWorkRoutine()); }
    private IEnumerator EndShiftRoutine() { yield return new WaitForSeconds(10f + Random.Range(-10f, 10f)); isWorking = false; currentState = ClerkState.Inactive; ClientQueueManager.SetServicePointAvailability(assignedServicePoint, false); ClientSpawner.ReportDeskOccupation(assignedServicePoint, null); SetPathTo(kitchenPoint.position); yield return new WaitUntil(() => Vector2.Distance(transform.position, targetPosition) < 0.2f); currentActionCoroutine = null; }
    private IEnumerator ReturnToWorkRoutine() { currentState = ClerkState.ReturningToWork; SetPathTo(workPoint.position); yield return new WaitUntil(() => Vector2.Distance(transform.position, targetPosition) < 0.2f); currentState = ClerkState.Working; ClientQueueManager.SetServicePointAvailability(assignedServicePoint, true); ClientSpawner.ReportDeskOccupation(assignedServicePoint, this); currentActionCoroutine = null; }
    private IEnumerator BreakRoutine(float duration) { currentState = ClerkState.GoingToBreak; ClientQueueManager.SetServicePointAvailability(assignedServicePoint, false); ClientSpawner.ReportDeskOccupation(assignedServicePoint, null); SetPathTo(kitchenPoint.position); yield return new WaitUntil(() => Vector2.Distance(transform.position, targetPosition) < 0.2f); currentState = ClerkState.OnBreak; yield return new WaitForSeconds(duration); yield return StartCoroutine(ReturnToWorkRoutine()); }
    
    private IEnumerator ToiletBreakRoutine()
    {
        currentState = ClerkState.GoingToToilet;
        ClientSpawner.ReportDeskOccupation(assignedServicePoint, null); 
        ClientQueueManager.SetServicePointAvailability(assignedServicePoint, false);
        SetPathTo(staffToiletPoint.position);
        yield return new WaitUntil(() => Vector2.Distance(transform.position, targetPosition) < 0.2f);
        currentState = ClerkState.AtToilet;
        yield return new WaitForSeconds(timeInToilet);
        yield return StartCoroutine(ReturnToWorkRoutine());
    }
    
    void SetPathTo(Vector2 finalDestination) { targetPosition = finalDestination; currentTargetWaypoint = FindNearestVisibleWaypoint(); }
    void MoveTowardsTarget() { if (Vector2.Distance(transform.position, targetPosition) < 0.2f) return; if (currentTargetWaypoint == null || Vector2.Distance(transform.position, currentTargetWaypoint.transform.position) < 0.2f) { Waypoint startNode = (currentTargetWaypoint == null) ? FindNearestVisibleWaypoint() : currentTargetWaypoint; if (startNode != null && startNode.neighbors != null && startNode.neighbors.Count > 0) { currentTargetWaypoint = startNode.neighbors.Where(n => n != null).OrderBy(n => Vector2.Distance(n.transform.position, targetPosition)).FirstOrDefault(); } else { rb.linearVelocity = (targetPosition - (Vector2)transform.position).normalized * moveSpeed; return; } } if (currentTargetWaypoint != null) { rb.linearVelocity = ((Vector2)currentTargetWaypoint.transform.position - (Vector2)transform.position).normalized * moveSpeed; } }
    void UpdateSpriteDirection() { if (rb.linearVelocity.x > 0.1f) spriteRenderer.flipX = false; else if (rb.linearVelocity.x < -0.1f) spriteRenderer.flipX = true; }
    private Waypoint FindNearestVisibleWaypoint() { if (allWaypoints == null) return null; Waypoint bestWaypoint = null; float minDistance = float.MaxValue; foreach (var wp in allWaypoints) { if (wp == null) continue; float distance = Vector2.Distance(transform.position, wp.transform.position); if (distance < minDistance) { RaycastHit2D hit = Physics2D.Linecast(transform.position, wp.transform.position, LayerMask.GetMask("Obstacles")); if (hit.collider == null) { minDistance = distance; bestWaypoint = wp; } } } return bestWaypoint; }
}