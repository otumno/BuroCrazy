// Файл: GuardManager.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GuardManager : MonoBehaviour
{
    public static GuardManager Instance { get; private set; }

    [Header("Настройки менеджера")]
    [Tooltip("Шанс (0-1), с которым охранник заметит попытку кражи")]
    public float chanceToNoticeTheft = 0.6f;
    [Tooltip("Перетащите сюда объект барьера со сцены")]
    public SecurityBarrier securityBarrier;

    private List<GuardMovement> allGuards = new List<GuardMovement>();
    private HashSet<ClientPathfinding> targetsBeingHandled = new HashSet<ClientPathfinding>();
	private List<ClientPathfinding> reportedThieves = new List<ClientPathfinding>();
    private List<ClientPathfinding> reportedViolators = new List<ClientPathfinding>();
    private List<ClientPathfinding> clientsToEvict = new List<ClientPathfinding>();
    
    private string lastCheckedPeriodName = "";
    private bool morningTaskAssigned = false;
    private bool nightTaskAssigned = false;

void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }

    void Start()
    {
        allGuards = FindObjectsByType<GuardMovement>(FindObjectsSortMode.None).ToList();
    }

    void Update()
    {
        // Убираем из "занятых" тех, кто уже не является целью (например, успокоился или ушел)
        targetsBeingHandled.RemoveWhere(client => client == null || 
                                           (client.stateMachine.GetCurrentState() != ClientState.Enraged && 
                                            client.reasonForLeaving != ClientPathfinding.LeaveReason.Theft));
        
        // Старые методы ManageChasing, ManageTheftIntervention и т.д. нам больше не нужны,
        // так как логика теперь работает через "мозг" охранника.
        // Оставляем только управление барьером.
        ManageBarrierTasks();
    }
    
    public void ReportTheft(ClientPathfinding thief)
    {
        if (thief != null && !targetsBeingHandled.Contains(thief))
        {
            Debug.Log($"[GuardManager] ПОЛУЧЕН СИГНАЛ: Кража от {thief.name}!");
            
            // Находим ЛЮБОГО охранника на смене (даже если он занят патрулем)
            GuardMovement guard = FindBestGuardForTask(thief.transform.position);
            if (guard != null)
            {
                targetsBeingHandled.Add(thief);
                guard.InterruptWithNewTask(thief, true); // Вызываем прерывание
            }
        }
    }

private GuardMovement FindBestGuardForTask(Vector3 targetPosition)
    {
        // Ищем ближайшего охранника, который:
        // 1. Существует и активен (g != null)
        // 2. Находится на смене (g.IsOnDuty())
        // 3. НЕ находится на перерыве или в туалете (!g.IsOnBreak())
        return allGuards
            .Where(g => g != null && g.IsOnDuty() && !g.IsOnBreak()) 
            .OrderBy(g => Vector3.Distance(g.transform.position, targetPosition))
            .FirstOrDefault();
    }


    public void ReportViolator(ClientPathfinding violator)
    {
        if (violator != null && !targetsBeingHandled.Contains(violator))
        {
            Debug.Log($"[GuardManager] ПОЛУЧЕН СИГНАЛ: Буйство от {violator.name}!");
            
            GuardMovement guard = FindBestGuardForTask(violator.transform.position);
            if (guard != null)
            {
                targetsBeingHandled.Add(violator);
                guard.InterruptWithNewTask(violator, false); // Вызываем прерывание
            }
        }
    }
	
	public ClientPathfinding GetThiefToCatch()
    {
        reportedThieves.RemoveAll(t => t == null); // Чистим список от уничтоженных объектов
        return reportedThieves.FirstOrDefault(t => !targetsBeingHandled.Contains(t));
    }

    public ClientPathfinding GetViolatorToHandle()
    {
        reportedViolators.RemoveAll(v => v == null);
        return reportedViolators.FirstOrDefault(v => !targetsBeingHandled.Contains(v));
    }
	
	public void AssignTarget(ClientPathfinding target)
    {
        if (target != null)
        {
            targetsBeingHandled.Add(target);
        }
    }
	
    public void EvictRemainingClients()
    {
        var remainingClients = FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).ToList();
        if (remainingClients.Count > 0)
        {
            Debug.Log($"[GuardManager] Рабочий день окончен. {remainingClients.Count} клиентов осталось на месте. Начинаем выпроваживать.");
            clientsToEvict.AddRange(remainingClients);
        }
    }

    private void ManageBarrierTasks()
    {
        if (securityBarrier == null || ClientSpawner.Instance == null) return;

        string currentPeriod = ClientSpawner.CurrentPeriodName;

        if (currentPeriod != lastCheckedPeriodName)
        {
            morningTaskAssigned = false;
            nightTaskAssigned = false;
            lastCheckedPeriodName = currentPeriod;
        }

        if (currentPeriod == "Утро" && !morningTaskAssigned && securityBarrier.IsActive())
        {
            var availableGuard = allGuards.FirstOrDefault(g => g.IsAvailableAndOnDuty());
            if (availableGuard != null)
            {
                Debug.Log($"GuardManager: Назначаю {availableGuard.name} на ДЕАКТИВАЦИЮ барьера.");
                availableGuard.AssignToOperateBarrier(securityBarrier, false);
                morningTaskAssigned = true;
            }
        }

        if (currentPeriod == "Ночь" && !nightTaskAssigned && !securityBarrier.IsActive())
        {
            if (FindObjectsByType<ClientPathfinding>(FindObjectsSortMode.None).Length == 0)
            {
                var availableGuard = allGuards.FirstOrDefault(g => g.IsAvailableAndOnDuty());
                if (availableGuard != null)
                {
                    Debug.Log($"GuardManager: Клиентов нет. Назначаю {availableGuard.name} на АКТИВАЦИЮ барьера.");
                    availableGuard.AssignToOperateBarrier(securityBarrier, true);
                    nightTaskAssigned = true;
                }
            }
        }
    }

    private void ManageChasing()
    {
        if (ClientQueueManager.Instance == null) return;
        var unassignedViolators = ClientQueueManager.Instance.dissatisfiedClients
            .Where(v => v != null && !targetsBeingHandled.Contains(v)).ToList();
        if (unassignedViolators.Count == 0) return;

        var availableGuards = allGuards.Where(g => g != null && g.IsAvailableAndOnDuty()).ToList();
        if (availableGuards.Count == 0) return;

        foreach (var violator in unassignedViolators)
        {
            if (availableGuards.Any())
            {
                GuardMovement closestGuard = availableGuards
                    .OrderBy(g => Vector2.Distance(g.transform.position, violator.transform.position))
                    .FirstOrDefault();
                if (closestGuard != null)
                {
                    Debug.Log($"[GuardManager] Охранник {closestGuard.name} назначен на усмирение {violator.name}");
                    closestGuard.AssignToChase(violator);
                    targetsBeingHandled.Add(violator); 
                    availableGuards.Remove(closestGuard);
                }
            }
            else break;
        }
    }
    
    private void ManageTheftIntervention()
    {
        if (reportedThieves.Count == 0) return;
        var availableGuards = allGuards.Where(g => g != null && g.IsAvailableAndOnDuty()).ToList();
        if (availableGuards.Count == 0) return;

        for (int i = reportedThieves.Count - 1; i >= 0; i--)
        {
            ClientPathfinding thief = reportedThieves[i];
            if (thief == null || targetsBeingHandled.Contains(thief))
            {
                reportedThieves.RemoveAt(i);
                continue;
            }

            if (Random.value < chanceToNoticeTheft)
            {
                GuardMovement closestGuard = availableGuards
                    .OrderBy(g => Vector2.Distance(g.transform.position, thief.transform.position))
                    .FirstOrDefault();
                if (closestGuard != null)
                {
                    closestGuard.AssignToCatchThief(thief);
                    targetsBeingHandled.Add(thief);
                    availableGuards.Remove(closestGuard);
                    reportedThieves.RemoveAt(i);
                }
            }
            else
            {
                Debug.Log($"Охрана не заметила, как {thief.name} пытается уйти, не заплатив!");
                reportedThieves.RemoveAt(i);
            }

            if (availableGuards.Count == 0) break;
        }
    }

    private void ManageEvictions()
    {
        var unassignedEvictees = clientsToEvict.Where(c => c != null && !targetsBeingHandled.Contains(c)).ToList();
        if (unassignedEvictees.Count == 0) return;
        
        var availableGuards = allGuards.Where(g => g != null && g.IsAvailableAndOnDuty()).ToList();
        if (availableGuards.Count == 0) return;

        foreach (var clientToEvict in unassignedEvictees)
        {
            if (availableGuards.Any())
            {
                GuardMovement closestGuard = availableGuards.OrderBy(g => Vector2.Distance(g.transform.position, clientToEvict.transform.position)).FirstOrDefault();
                if (closestGuard != null)
                {
                    closestGuard.AssignToEvict(clientToEvict);
                    targetsBeingHandled.Add(clientToEvict);
                    availableGuards.Remove(closestGuard);
                }
            }
            else break;
        }
    }

    public void ReportTaskFinished(ClientPathfinding target)
    {
        if (target != null)
        {
            targetsBeingHandled.Remove(target);
        }
    }
	

/// <summary>
/// Находит клиента для выпроваживания, которым еще не занимается другой охранник.
/// </summary>
public ClientPathfinding GetClientToEvict()
{
    clientsToEvict.RemoveAll(c => c == null);
    return clientsToEvict.FirstOrDefault(c => !targetsBeingHandled.Contains(c));
}
	
}