using UnityEngine;
using System.Collections;
using Utilities;
using Managers;

public class ClientActionExecutor : MonoBehaviour
{
    private ClientPathfinding _client;
    private AgentMover _mover;

    public void Initialize(ClientPathfinding client)
    {
        _client = client;
        _mover = GetComponent<AgentMover>(); // Executor сам находит AgentMover на том же объекте
    }

    // Полная остановка (и логики, и физики)
    public void StopAllActions()
    {
        StopAllCoroutines();
        StopMoving();
    }

    // --- ИСПРАВЛЕНИЕ 1: Добавлен метод StopMoving ---
    // Просто останавливает движение агента
    public void StopMoving()
    {
        _mover?.Stop();
        if (_client != null && _client.movement != null)
        {
            _client.movement.StopStuckCheck();
        }
    }

    public IEnumerator MoveToGoalRoutine(Waypoint goal)
    {
        if (goal == null)
        {
            Debug.LogWarning($"{name}: Цель движения null!");
            yield break;
        }

        _client.movement.StartStuckCheck();
        _mover.SetPath(PathfindingUtility.BuildPathTo(transform.position, goal.transform.position, gameObject));
        
        yield return new WaitUntil(() => !_mover.IsMoving());
        
        _client.movement.StopStuckCheck();
    }

    // --- ИСПРАВЛЕНИЕ 2: Добавлен метод MoveToSeatRoutine ---
    // Специальный метод для движения к Transform (так как стулья пока не Waypoint'ы)
    public IEnumerator MoveToSeatRoutine(Transform seatTarget)
    {
        if (seatTarget == null) yield break;

        _client.movement.StartStuckCheck();
        _mover.SetPath(PathfindingUtility.BuildPathTo(transform.position, seatTarget.position, gameObject));
        
        yield return new WaitUntil(() => !_mover.IsMoving());
        
        _client.movement.StopStuckCheck();
    }

    public IEnumerator EnterZoneRoutine(LimitedCapacityZone targetZone)
    {
        if (targetZone == null || _client == null) yield break;

        _mover.Stop();

        // Логика очереди
        if (_client.hasBeenSentForRevision)
        {
            targetZone.JumpQueue(_client.gameObject);
            _client.hasBeenSentForRevision = false;
        }
        else
        {
            targetZone.JoinQueue(_client.gameObject);
        }

        // Ждем, пока станем первыми или клиент не будет уничтожен
        yield return new WaitUntil(() => _client == null || targetZone.IsFirstInQueue(_client.gameObject));
        
        if (_client == null)
        {
            targetZone.LeaveQueue(gameObject);
            yield break;
        }

        Waypoint freeSpot = null;
        float zoneTimeout = 30f;

        while (freeSpot == null && zoneTimeout > 0)
        {
            if (_client == null)
            {
                targetZone.LeaveQueue(gameObject);
                yield break;
            }
            
            if (CanEnterZone(targetZone))
            {
                freeSpot = targetZone.RequestAndOccupyWaypoint(_client.gameObject);
            }
            if (freeSpot == null)
            {
                yield return new WaitForSeconds(0.5f);
                zoneTimeout -= 0.5f;
            }
        }

        if (freeSpot == null)
        {
            targetZone.LeaveQueue(gameObject);
            _client?.stateMachine?.SetState(ClientState.Confused);
            yield break;
        }

        if (_client == null)
        {
            targetZone.LeaveQueue(gameObject);
            targetZone.ReleaseWaypoint(freeSpot);
            yield break;
        }

        targetZone.LeaveQueue(_client.gameObject);
        
        // Сообщаем стейт-машине, что вход разрешен и место занято
        _client.stateMachine.OnZoneEntryApproved(freeSpot, targetZone);
        
        // Спец. логика директора
        if ((_client.mainGoal == ClientGoal.DirectorApproval || _client.mainGoal == ClientGoal.DirectorAudience) && targetZone == ClientSpawner.Instance?.directorReceptionZone)
        {
            StartOfDayPanel.Instance?.RegisterDirectorDocument(_client);
        }
    }

    private bool CanEnterZone(LimitedCapacityZone zone)
    {
        int deskId = GetDeskIdFromZone(zone);
        if (deskId == int.MinValue) return true; // Туалет/Коридор

        IServiceProvider provider = ClientSpawner.GetServiceProviderAtDesk(deskId);
        return provider != null && provider.IsAvailableToServe;
    }

    private int GetDeskIdFromZone(LimitedCapacityZone zone)
    {
        if (zone == ClientSpawner.GetDesk1Zone()) return 1;
        if (zone == ClientSpawner.GetDesk2Zone()) return 2;
        if (zone == ClientSpawner.GetCashierZone()) return -1;
        if (zone == ClientSpawner.GetRegistrationZone()) return 0;
        return int.MinValue;
    }
}