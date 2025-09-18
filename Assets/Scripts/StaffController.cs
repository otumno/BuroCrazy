// Файл: StaffController.cs - ПОЛНАЯ ВЕРСИЯ
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public abstract class StaffController : MonoBehaviour
{
    // --- ENUM'Ы ДЛЯ СИСТЕМЫ ---
    public enum StaffType { Clerk, Guard, Intern, ServiceWorker }
    public enum Role { Unassigned, Intern, Registrar, Cashier, Archivist, Guard, Janitor }
    
    [Header("График работы")]
    [Tooltip("Настройте рабочие периоды ниже с помощью галочек.")]
    public List<string> workPeriods;
    
    [Header("Стандартные точки")]
    public Transform homePoint;
    [Tooltip("Список всех возможных точек для отдыха на кухне.")]
    public List<Transform> kitchenPoints;
    [Tooltip("Точка входа (ожидания) в зону туалета для персонала.")]
    public Transform staffToiletPoint;
    
    [Header("Звуки смены")]
    public AudioClip startShiftSound;
    public AudioClip endShiftSound;

    [Header("Прогрессия и Роль")]
	    [Tooltip("Имя персонажа, которое будет отображаться в UI")]
		public string characterName = "Безымянный";
	public Gender gender;
    public CharacterSkills skills;
    public Role currentRole = Role.Intern;
    public int rank = 0;
    public int experiencePoints = 0;
    public int salaryPerPeriod = 15; // Базовая зарплата за один рабочий период
	public bool isReadyForPromotion = false; // <--- ДОБАВЬТЕ ЭТУ СТРОКУ

    protected bool isOnDuty = false;
    protected Coroutine currentAction;
    protected AgentMover agentMover;
    protected CharacterStateLogger logger;

    private static List<Transform> occupiedKitchenPoints = new List<Transform>();

    public bool IsOnDuty() => isOnDuty;
    public abstract void StartShift();
    public abstract void EndShift();
    public abstract void GoOnBreak(float duration);

    protected virtual void Awake()
    {
        agentMover = GetComponent<AgentMover>();
        logger = GetComponent<CharacterStateLogger>();
    }

    protected virtual void Start()
    {
        if (homePoint != null)
        {
            transform.position = homePoint.position;
        }
    }

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