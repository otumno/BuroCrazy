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

    private List<GuardMovement> allGuards = new List<GuardMovement>();
    private List<ClientPathfinding> assignedViolators = new List<ClientPathfinding>();
    private List<ClientPathfinding> reportedThieves = new List<ClientPathfinding>();
    private List<ClientPathfinding> clientsToEvict = new List<ClientPathfinding>();
    private List<ClientPathfinding> assignedEvictees = new List<ClientPathfinding>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }

    void Start()
    {
        // Находим всех охранников на сцене при старте
        allGuards = FindObjectsByType<GuardMovement>(FindObjectsSortMode.None).ToList();
    }

    void Update()
    {
        ManageChasing();
        ManageTheftIntervention();
        ManageEvictions();
    }
    
    public void ReportTheft(ClientPathfinding thief)
    {
        if (!reportedThieves.Contains(thief))
        {
            Debug.Log($"[GuardManager] Получено сообщение о попытке кражи клиентом {thief.name}!");
            reportedThieves.Add(thief);
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

    private void ManageChasing()
    {
        if (ClientQueueManager.Instance == null) return;
        var unassignedViolators = ClientQueueManager.Instance.dissatisfiedClients.Where(v => v != null && !assignedViolators.Contains(v)).ToList();
        if (unassignedViolators.Count == 0) return;
        var availableGuards = allGuards.Where(g => g != null && g.IsAvailableAndOnDuty()).ToList();
        foreach (var violator in unassignedViolators)
        {
            if (availableGuards.Count > 0)
            {
                GuardMovement closestGuard = availableGuards.OrderBy(g => Vector2.Distance(g.transform.position, violator.transform.position)).FirstOrDefault();
                if (closestGuard != null)
                {
                    closestGuard.AssignToChase(violator);
                    assignedViolators.Add(violator);
                    availableGuards.Remove(closestGuard);
                }
            }
            else break;
        }
    }
    
    private void ManageTheftIntervention()
    {
        if (reportedThieves.Count == 0) return;
        ClientPathfinding thief = reportedThieves[0];
        if (thief == null)
        {
            reportedThieves.RemoveAt(0);
            return;
        }

        var availableGuards = allGuards.Where(g => g != null && g.IsAvailableAndOnDuty()).ToList();
        if (availableGuards.Count > 0)
        {
            if (Random.value < chanceToNoticeTheft)
            {
                GuardMovement closestGuard = availableGuards.OrderBy(g => Vector2.Distance(g.transform.position, thief.transform.position)).FirstOrDefault();
                if (closestGuard != null)
                {
                    closestGuard.AssignToCatchThief(thief);
                    reportedThieves.Remove(thief); 
                }
            }
            else
            {
                Debug.Log($"Охрана не заметила, как {thief.name} пытается уйти, не заплатив!");
                reportedThieves.Remove(thief); 
            }
        }
    }

    private void ManageEvictions()
    {
        if (clientsToEvict.Count == 0) return;
        var unassignedEvictees = clientsToEvict.Where(c => c != null && !assignedEvictees.Contains(c)).ToList();
        if (unassignedEvictees.Count == 0) return;
        var availableGuards = allGuards.Where(g => g != null && g.IsAvailableAndOnDuty()).ToList();
        if (availableGuards.Count == 0) return;

        foreach (var clientToEvict in unassignedEvictees)
        {
            if (availableGuards.Count > 0)
            {
                GuardMovement closestGuard = availableGuards.OrderBy(g => Vector2.Distance(g.transform.position, clientToEvict.transform.position)).FirstOrDefault();
                if (closestGuard != null)
                {
                    closestGuard.AssignToEvict(clientToEvict);
                    assignedEvictees.Add(clientToEvict);
                    availableGuards.Remove(closestGuard);
                }
            }
            else break;
        }
        
        clientsToEvict.RemoveAll(c => assignedEvictees.Contains(c));
    }

    public void ReportTaskFinished(ClientPathfinding target)
    {
        if (target != null)
        {
            if (assignedViolators.Contains(target)) assignedViolators.Remove(target);
            if (assignedEvictees.Contains(target)) assignedEvictees.Remove(target);
        }
    }
}