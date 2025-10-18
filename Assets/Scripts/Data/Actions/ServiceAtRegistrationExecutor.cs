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
        if (registrar == null || registrar.assignedWorkstation == null) 
        {
            FinishAction(false); 
            yield break;
        }

        var zone = ClientSpawner.GetZoneByDeskId(registrar.assignedWorkstation.deskId);
        var client = zone?.GetOccupyingClients().FirstOrDefault();

        if (client == null) 
        {
            FinishAction(false);
            yield break;
        }
        
        registrar.SetState(ClerkController.ClerkState.Working);
        
        bool canMakeArchiveRequest = registrar.activeActions.Any(a => a.actionType == ActionType.MakeArchiveRequest);

        if (client.mainGoal == ClientGoal.GetArchiveRecord && canMakeArchiveRequest)
        {
            yield return staff.StartCoroutine(HandleArchiveRequest(registrar, client));
        }
        else
        {
            yield return staff.StartCoroutine(HandleStandardRegistration(registrar, client));
        }
        
        registrar.ServiceComplete();
        ExperienceManager.Instance?.GrantXP(staff, actionData.actionType);
        FinishAction(true);
    }
    
    private IEnumerator HandleStandardRegistration(ClerkController registrar, ClientPathfinding client)
    {
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

        if (client.stateMachine.MyQueueNumber != -1)
        {
            ClientQueueManager.Instance.RemoveClientFromQueue(client);
        }
        
        client.stateMachine.SetGoal(actualDestination);
        client.stateMachine.SetState(ClientState.MovingToGoal);
    }
    
    private IEnumerator HandleArchiveRequest(ClerkController registrar, ClientPathfinding client)
    {
        ArchiveRequestManager.Instance.CreateRequest(registrar, client);
        registrar.SetState(ClerkController.ClerkState.WaitingForArchive);
        client.stateMachine.SetState(ClientState.WaitingForDocument);

        var archivistDesk = ScenePointsRegistry.Instance.GetServicePointByID(3); 
        if (archivistDesk == null) 
        { 
            // Отправляем клиента домой, если архивариуса нет
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            client.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
            client.stateMachine.SetState(ClientState.LeavingUpset);
            yield break; 
        }

        yield return staff.StartCoroutine(registrar.MoveToTarget(archivistDesk.clerkStandPoint.position, ClerkController.ClerkState.WaitingForArchive.ToString()));

        float waitTimer = 0f;
        float maxWaitTime = 60f;
        bool requestFulfilled = false;
        var request = ArchiveRequestManager.Instance.GetNextRequest();
        while(waitTimer < maxWaitTime)
        {
            if (request != null && request.IsFulfilled)
            {
                requestFulfilled = true;
                break;
            }
            waitTimer += Time.deltaTime;
            yield return null;
        }

        if (requestFulfilled)
        {
            registrar.GetComponent<StackHolder>().ShowSingleDocumentSprite(); 
            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
            registrar.GetComponent<StackHolder>().HideStack();

            client.billToPay += 150; 
            client.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
            client.stateMachine.SetState(ClientState.MovingToGoal);
        }
        else
        {
            registrar.thoughtBubble?.ShowPriorityMessage("Архив не отвечает...\nИзвините.", 3f, Color.red);
            registrar.SetState(ClerkController.ClerkState.ReturningToWork);
            yield return staff.StartCoroutine(registrar.MoveToTarget(registrar.assignedWorkstation.clerkStandPoint.position, ClerkController.ClerkState.Working.ToString()));
            
            client.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
            client.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
            client.stateMachine.SetState(ClientState.LeavingUpset);
        }
    }

    private Waypoint DetermineCorrectGoalForClient(ClientPathfinding client)
    {
        if (client.billToPay > 0) return ClientSpawner.GetCashierZone().waitingWaypoint;
        switch (client.mainGoal)
        {
            case ClientGoal.PayTax: return ClientSpawner.GetCashierZone().waitingWaypoint;
            case ClientGoal.GetCertificate1: return ClientSpawner.GetDesk1Zone().waitingWaypoint;
            case ClientGoal.GetCertificate2: return ClientSpawner.GetDesk2Zone().waitingWaypoint;
            default: 
                client.isLeavingSuccessfully = true;
                client.reasonForLeaving = ClientPathfinding.LeaveReason.Processed;
                return ClientSpawner.Instance.exitWaypoint;
        }
    }
}