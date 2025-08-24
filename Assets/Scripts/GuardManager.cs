using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GuardManager : MonoBehaviour
{
    public static GuardManager Instance { get; private set; }

    private List<GuardMovement> allGuards = new List<GuardMovement>();
    private List<ClientPathfinding> assignedViolators = new List<ClientPathfinding>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        // Находим всех охранников на сцене при старте
        allGuards = FindObjectsByType<GuardMovement>(FindObjectsSortMode.None).ToList();
    }

    void Update()
    {
        // Ищем нарушителей, на которых еще не назначен охранник
        var unassignedViolators = ClientQueueManager.dissatisfiedClients
            .Where(v => v != null && !assignedViolators.Contains(v))
            .ToList();

        if (unassignedViolators.Count == 0) return;

        // Ищем свободных охранников
        var availableGuards = allGuards.Where(g => g.IsAvailable()).ToList();

        // Назначаем свободных охранников на свободных нарушителей
        foreach (var violator in unassignedViolators)
        {
            if (availableGuards.Count > 0)
            {
                // Находим ближайшего свободного охранника
                GuardMovement closestGuard = availableGuards
                    .OrderBy(g => Vector2.Distance(g.transform.position, violator.transform.position))
                    .FirstOrDefault();

                if (closestGuard != null)
                {
                    closestGuard.AssignToChase(violator);
                    assignedViolators.Add(violator);
                    availableGuards.Remove(closestGuard); // Этот охранник больше не свободен
                }
            }
            else
            {
                break; // Свободные охранники закончились
            }
        }
    }

    // Метод, который охранник вызовет, когда закончит с нарушителем
    public void ReportTaskFinished(ClientPathfinding violator)
    {
        if (violator != null && assignedViolators.Contains(violator))
        {
            assignedViolators.Remove(violator);
        }
    }
}