using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class GuardManager : MonoBehaviour
{
    public static GuardManager Instance { get; private set; }

    [Header("Настройки менеджера")]
    public List<Transform> guardPosts;

    private List<GuardMovement> allGuards = new List<GuardMovement>();
    private List<ClientPathfinding> assignedViolators = new List<ClientPathfinding>();
    private string lastPeriod = "";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }

    IEnumerator Start()
    {
        allGuards = FindObjectsByType<GuardMovement>(FindObjectsSortMode.None).ToList();
        AssignShifts();
        yield return new WaitForSeconds(0.5f); 
        OnPeriodChange(ClientSpawner.CurrentPeriodName);
    }

    void Update()
    {
        string currentPeriod = ClientSpawner.CurrentPeriodName;
        if (currentPeriod != lastPeriod)
        {
            OnPeriodChange(currentPeriod);
            lastPeriod = currentPeriod;
        }
        ManageChasing();
    }

    private void AssignShifts()
    {
        if (allGuards.Count == 0) return;
        if (allGuards.Count == 1)
        {
            allGuards[0].AssignShift(GuardMovement.Shift.Universal);
            Debug.Log($"{allGuards[0].name} назначен на универсальную смену.");
            return;
        }

        int dayGuards = 0;
        int nightGuards = 0;
        for (int i = 0; i < allGuards.Count; i++)
        {
            if (dayGuards <= nightGuards)
            {
                allGuards[i].AssignShift(GuardMovement.Shift.Day);
                dayGuards++;
                Debug.Log($"{allGuards[i].name} назначен на ДНЕВНУЮ смену.");
            }
            else
            {
                allGuards[i].AssignShift(GuardMovement.Shift.Night);
                nightGuards++;
                Debug.Log($"{allGuards[i].name} назначен на НОЧНУЮ смену.");
            }
        }
    }
    
    private void OnPeriodChange(string period)
    {
        if (string.IsNullOrEmpty(period)) return;
        string p = period.ToLower().Trim();
        Debug.Log($"[GuardManager] Наступил новый период: {p}. Отдаю команды охране.");

        bool isDayTime = (p == "утро" || p == "начало дня" || p == "день" || p == "обед" || p == "конец дня" || p == "вечер");

        foreach (var guard in allGuards)
        {
            if (guard == null) continue;

            bool isDayGuard = guard.assignedShift == GuardMovement.Shift.Day || guard.assignedShift == GuardMovement.Shift.Universal;
            bool isNightGuard = guard.assignedShift == GuardMovement.Shift.Night;

            // --- ОБНОВЛЕННАЯ ЛОГИКА УПРАВЛЕНИЯ СМЕНАМИ И ПЕРЕРЫВАМИ ---
            
            // Если сейчас день и дневной охранник не на смене (и не на обеде) -> Начать смену
            if (isDayTime && isDayGuard && !guard.IsAvailableAndOnDuty() && guard.GetCurrentState() == GuardMovement.GuardState.OffDuty)
            {
                guard.StartShift();
            }
            // Если сейчас ночь и дневной охранник на смене -> Закончить смену
            else if (!isDayTime && isDayGuard && guard.IsAvailableAndOnDuty())
            {
                guard.EndShift();
            }
            // Аналогично для ночного охранника
            else if (!isDayTime && isNightGuard && guard.GetCurrentState() == GuardMovement.GuardState.OffDuty)
            {
                guard.StartShift();
            }
            else if (isDayTime && isNightGuard && guard.IsAvailableAndOnDuty())
            {
                guard.EndShift();
            }
            
            // Логика для обеда
            if (p == "обед" && guard.IsAvailableAndOnDuty())
            {
                 if (guardPosts != null && guardPosts.Count > 0)
                 {
                     guard.GoOnBreak(guardPosts[0]);
                 }
            }
            // Логика возвращения с обеда
            else if (p != "обед" && guard.GetCurrentState() == GuardMovement.GuardState.OnBreak)
            {
                guard.ReturnToPatrol();
            }
        }
    }

    private void ManageChasing()
    {
        var unassignedViolators = ClientQueueManager.dissatisfiedClients.Where(v => v != null && !assignedViolators.Contains(v)).ToList();
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

    public void ReportTaskFinished(ClientPathfinding violator)
    {
        if (violator != null && assignedViolators.Contains(violator)) { assignedViolators.Remove(violator); }
    }
}