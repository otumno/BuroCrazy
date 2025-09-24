using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public abstract class StaffController : MonoBehaviour
{
    public enum Role { Unassigned, Intern, Registrar, Cashier, Archivist, Guard, Janitor, Clerk }
    
    [Header("График работы")]
    public List<string> workPeriods;
    
    [Header("Патрулирование (для ролей, где это применимо)")]
    public List<Transform> patrolPoints;

    [Header("Стандартные точки")]
    public Transform homePoint;
    public List<Transform> kitchenPoints;
    public Transform staffToiletPoint;

    [Header("Звуки смены")]
    public AudioClip startShiftSound;
    public AudioClip endShiftSound;

    [Header("Прогрессия и Роль")]
	public string characterName = "Безымянный";
	public Gender gender;
    public CharacterSkills skills;
    public Role currentRole = Role.Intern;
    public int rank = 0;
    public int experiencePoints = 0;
    public int salaryPerPeriod = 15;
	public bool isReadyForPromotion = false;
    public List<StaffAction> activeActions = new List<StaffAction>();

    protected bool isOnDuty = false;
    protected Coroutine currentAction;
    protected AgentMover agentMover;
    protected CharacterStateLogger logger;
    protected CharacterVisuals visuals; // Добавлено для общего доступа

    private static List<Transform> occupiedKitchenPoints = new List<Transform>();
    private bool isInitialized = false;

    // --- ОСНОВНЫЕ МЕТОДЫ УПРАВЛЕНИЯ ---

    public bool IsOnDuty() => isOnDuty;

    public virtual void StartShift()
    {
        if (isOnDuty) return;
        isOnDuty = true;
        if (startShiftSound != null) AudioSource.PlayClipAtPoint(startShiftSound, transform.position);
        
        // Запускаем "мозг" сотрудника
        currentAction = StartCoroutine(ActionDecisionLoop());
    }

    public virtual void EndShift()
    {
        if (!isOnDuty) return;
        isOnDuty = false;
        
        // Останавливаем "мозг"
        if (currentAction != null)
        {
            StopCoroutine(currentAction);
            currentAction = null;
        }
        
        // Запускаем процесс ухода домой (но не удаления)
        StartCoroutine(GoHomeRoutine());
    }
    
    public void FireAndGoHome()
    {
        isOnDuty = false;
        if (currentAction != null)
        {
            StopCoroutine(currentAction);
            currentAction = null;
        }
        StartCoroutine(GoHomeAndDespawnRoutine());
    }
    
    public abstract void GoOnBreak(float duration);
    
    // --- "МОЗГ" AI ---

    private IEnumerator ActionDecisionLoop()
    {
        while (isOnDuty)
        {
            // Убеждаемся, что никакое другое действие (например, перерыв) не выполняется
            if (currentAction == null)
            {
                bool actionFoundAndExecuted = false;
                
                if (activeActions.Any())
                {
                    foreach (var action in activeActions)
                    {
                        if (TryExecuteAction(action.actionType))
                        {
                            actionFoundAndExecuted = true;
                            yield return new WaitUntil(() => currentAction == null);
                            break; 
                        }
                    }
                }

                if (!actionFoundAndExecuted)
                {
                    ExecuteDefaultAction();
                    yield return new WaitUntil(() => currentAction == null);
                }
            }

            yield return new WaitForSeconds(Random.Range(0.8f, 1.2f));
        }
    }

    protected virtual bool TryExecuteAction(ActionType actionType)
    {
        // Дочерние классы (Clerk, Guard) будут переопределять этот метод
        return false;
    }

    protected virtual void ExecuteDefaultAction()
    {
        // Дочерние классы будут переопределять этот метод
    }
    
    // --- РУТИНЫ И ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---
    
    private IEnumerator GoHomeRoutine()
    {
        if (homePoint != null)
        {
            agentMover.SetPath(BuildPathTo(homePoint.position));
            yield return new WaitUntil(() => !agentMover.IsMoving());
            if (endShiftSound != null) AudioSource.PlayClipAtPoint(endShiftSound, homePoint.position);
        }
    }
    
    private IEnumerator GoHomeAndDespawnRoutine()
    {
        Debug.Log($"Сотрудник {characterName} уволен и идет домой...");
        yield return StartCoroutine(GoHomeRoutine());
        Debug.Log($"{characterName} дошел до точки ухода и исчезает.");
        Destroy(gameObject);
    }
    
    // --- МЕТОДЫ UNITY (AWAKE, START) ---

    protected virtual void Awake()
    {
        agentMover = GetComponent<AgentMover>();
        logger = GetComponent<CharacterStateLogger>();
        visuals = GetComponent<CharacterVisuals>();
    }

    protected virtual void Start()
    {
        if (!isInitialized)
        {
            if (homePoint != null)
            {
                transform.position = homePoint.position;
            }
            InitializeRole();
            isInitialized = true;
        }
    }

    private void InitializeRole()
    {
        if (this.currentRole == Role.Intern)
        {
            // Для стажера просто инициализируем визуал
            var internController = GetComponent<InternController>();
            if (internController != null && visuals != null)
            {
                visuals.Setup(this.gender, internController.spriteCollection, internController.stateEmotionMap);
            }
            return;
        }
        
        Role startingRole = this.currentRole;
        this.currentRole = Role.Intern; 
        HiringManager.Instance.AssignNewRole(this, startingRole);
    }

    // --- ОСТАЛЬНЫЕ МЕТОДЫ ---

    protected Transform RequestKitchenPoint()
    {
        if (kitchenPoints == null || kitchenPoints.Count == 0) return null;
        var randomizedPoints = kitchenPoints.OrderBy(p => Random.value).ToList();
        foreach (var point in randomizedPoints)
        {
            if (!occupiedKitchenPoints.Contains(point))
            {
                occupiedKitchenPoints.Add(point);
                return point;
            }
        }
        return randomizedPoints.FirstOrDefault();
    }

    protected void FreeKitchenPoint(Transform point)
    {
        if (point != null && occupiedKitchenPoints.Contains(point))
        {
            occupiedKitchenPoints.Remove(point);
        }
    }

    protected IEnumerator EnterLimitedZoneAndWaitRoutine(Transform zoneEntrance, float waitDuration)
    {
        if (zoneEntrance == null)
        {
            Debug.LogError($"{name} не может войти в зону, так как точка входа не задана!");
            yield break;
        }
        LimitedCapacityZone zone = zoneEntrance.GetComponentInParent<LimitedCapacityZone>();
        if (zone == null)
        {
            Debug.LogError($"{name} пытается использовать {zoneEntrance.name} как вход в зону, но на родительском объекте нет компонента LimitedCapacityZone!");
            yield return new WaitForSeconds(waitDuration);
            yield break;
        }
        zone.JoinQueue(gameObject);
        yield return new WaitUntil(() => zone.IsFirstInQueue(gameObject));
        Waypoint insidePoint = null;
        while (insidePoint == null)
        {
            if (this == null || !gameObject.activeInHierarchy) yield break;
            insidePoint = zone.RequestAndOccupyWaypoint(gameObject);
            if (insidePoint == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }
        zone.LeaveQueue(gameObject);
        agentMover.SetPath(BuildPathTo(insidePoint.transform.position));
        yield return new WaitUntil(() => !agentMover.IsMoving());
        yield return new WaitForSeconds(waitDuration);
        zone.ReleaseWaypoint(insidePoint);
        if (zone.exitWaypoint != null)
        {
            agentMover.SetPath(BuildPathTo(zone.exitWaypoint.transform.position));
            yield return new WaitUntil(() => !agentMover.IsMoving());
        }
    }
    
    protected abstract Queue<Waypoint> BuildPathTo(Vector2 targetPos);
    public virtual float GetStressValue() { return 0f; }
    public virtual void SetStressValue(float stress) { }
}