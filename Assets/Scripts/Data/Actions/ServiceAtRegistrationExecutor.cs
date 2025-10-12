using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class ServiceAtRegistrationExecutor : ActionExecutor
{
    public override bool IsInterruptible => false;

    protected override IEnumerator ActionRoutine()
    {
        var registrar = staff as ClerkController;
        // Используем 'assignedWorkstation'
        if (registrar == null || registrar.assignedWorkstation == null)
        {
            FinishAction();
            yield break;
        }

        // Используем 'assignedWorkstation'
        var zone = ClientSpawner.GetZoneByDeskId(registrar.assignedWorkstation.deskId);
        var client = zone?.GetOccupyingClients().FirstOrDefault();

        if (client == null)
        {
            FinishAction();
            yield break;
        }
        
        registrar.SetState(ClerkController.ClerkState.Working);
        yield return new WaitForSeconds(Random.Range(1f, 2.5f));
        
        Waypoint correctDestination = DetermineCorrectGoalForClient(client);
        Waypoint actualDestination = correctDestination; 

        float baseSuccessChance = 0.7f;
        float clientModifier = (client.babushkaFactor * 0.1f) - (client.suetunFactor * 0.2f);
        float registrarBonus = registrar.redirectionBonus;
        float finalChance = Mathf.Clamp(baseSuccessChance + clientModifier + registrarBonus, 0.3f, 0.95f);

        if (Random.value > finalChance)
        {
            Debug.LogWarning($"[Registration] ПРОВАЛ НАПРАВЛЕНИЯ! Регистратор {registrar.name} ошибся с клиентом {client.name}. Шанс был {finalChance:P0}");
            List<Waypoint> possibleDestinations = new List<Waypoint> 
            { 
                ClientSpawner.GetQuietestZone(ClientSpawner.Instance.category1DeskZones)?.waitingWaypoint,
                ClientSpawner.GetQuietestZone(ClientSpawner.Instance.category2DeskZones)?.waitingWaypoint,
                ClientSpawner.GetQuietestZone(ClientSpawner.Instance.cashierZones)?.waitingWaypoint,
                ClientSpawner.Instance.toiletZone.waitingWaypoint,
                ClientQueueManager.Instance.ChooseNewGoal(client)
            };
            possibleDestinations.RemoveAll(item => item == null || item == correctDestination);
            if (possibleDestinations.Count > 0)
            {
                actualDestination = possibleDestinations[Random.Range(0, possibleDestinations.Count)];
            }
        }
        
        string destinationName = string.IsNullOrEmpty(actualDestination.friendlyName) ? actualDestination.name : actualDestination.friendlyName;
        string directionMessage = $"Пройдите, пожалуйста, к\n'{destinationName}'";
        registrar.thoughtBubble?.ShowPriorityMessage(directionMessage, 3f, Color.white);

        Debug.Log($"[Registration] Клиент {client.name} направлен к {actualDestination.name}.");
        
        if (client.stateMachine.MyQueueNumber != -1)
        {
            ClientQueueManager.Instance.RemoveClientFromQueue(client);
        }
        
        client.stateMachine.SetGoal(actualDestination);
        client.stateMachine.SetState(ClientState.MovingToGoal);
        
        registrar.ServiceComplete();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction();
    }
    
    private Waypoint DetermineCorrectGoalForClient(ClientPathfinding client)
    {
        if (client.billToPay > 0) return ClientSpawner.GetCashierZone().waitingWaypoint;
        switch (client.mainGoal)
        {
            case ClientGoal.PayTax: return ClientSpawner.GetCashierZone().waitingWaypoint;
            case ClientGoal.GetCertificate1: return ClientSpawner.GetDesk1Zone().waitingWaypoint;
            case ClientGoal.GetCertificate2: return ClientSpawner.GetDesk2Zone().waitingWaypoint;
            case ClientGoal.AskAndLeave:
            default: 
                client.isLeavingSuccessfully = true;
                client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                return ClientSpawner.Instance.exitWaypoint;
        }
    }
}