using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run with Unity CLI: command 'script run' --file Tools/Checks/ClientZoneReservation.cs
// Uses an isolated preview scene; does not start Play Mode or save scene/assets.
public static class ClientZoneReservationCheck
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("ClientZoneReservationCheck");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var zone = Child(root, "Zone").AddComponent<LimitedCapacityZone>();
            var spot = Child(zone.gameObject, "Spot").AddComponent<Waypoint>();
            var outside = Child(root, "Outside").AddComponent<Waypoint>();
            zone.insideWaypoints = new List<Waypoint> { spot };
            zone.capacity = 1;
            var visitor = Child(root, "Visitor").AddComponent<ClientStateMachine>();
            var client = visitor.GetComponent<ClientPathfinding>();
            client.stateMachine = visitor;
            Set(visitor, "parent", client);
            Set(visitor, "actionDiary", visitor.GetComponent<ActionDiary>());
            Set(visitor, "feedbackController", visitor.GetComponent<ClientFeedbackController>());
            var second = Child(root, "SecondVisitor");
            root.SetActive(true);

            visitor.SetGoal(outside);
            visitor.SetState(ClientState.AtLimitedZoneEntrance);
            zone.JoinQueue(visitor.gameObject);
            var reserved = zone.RequestAndOccupyWaypoint(visitor.gameObject);
            zone.LeaveQueue(visitor.gameObject);
            visitor.OnZoneEntryApproved(reserved, zone);
            Require(zone.GetCurrentOccupancy() == 1, "Admission lost its reservation while walking.");
            Require(zone.RequestAndOccupyWaypoint(second) == null, "Second visitor took the reserved spot.");
            Require(zone.GetOccupyingClients().Count == 0, "Service started before arrival.");
            visitor.SetState(ClientState.InsideLimitedZone);
            Require(zone.GetOccupyingClients().Count == 1, "Arrived visitor is invisible to service actions.");
            visitor.transform.position = new Vector3(10f, 0f, 0f);
            Require(zone.GetOccupyingClients().Count == 0, "Distant visitor can be served remotely.");
            visitor.transform.position = spot.transform.position;
            visitor.SetState(ClientState.AtDesk1);
            visitor.SetState(ClientState.WaitingForDocument);
            Require(zone.GetCurrentOccupancy() == 1, "Service/document wait released the occupied spot.");
            visitor.SetGoal(outside);
            Require(zone.GetCurrentOccupancy() == 0 && visitor.GetTargetZone() == null,
                "Changing destination did not release the old zone.");

            visitor.SetState(ClientState.AtLimitedZoneEntrance);
            visitor.OnZoneEntryApproved(zone.RequestAndOccupyWaypoint(visitor.gameObject), zone);
            visitor.SetState(ClientState.LeavingUpset);
            Require(zone.GetCurrentOccupancy() == 0, "Abandoning the route leaked a reservation.");

            visitor.SetState(ClientState.AtLimitedZoneEntrance);
            visitor.OnZoneEntryApproved(zone.RequestAndOccupyWaypoint(visitor.gameObject), zone);
            zone.ReleaseWaypoint(spot);
            Require(zone.RequestAndOccupyWaypoint(second) == spot, "Second visitor could not enter after release.");
            client.ForceLeave();
            visitor.ReleaseZoneReservation();
            Require(zone.IsWaypointOccupied(spot), "Stale cleanup evicted the second visitor.");

            visitor.SetState(ClientState.AtLimitedZoneEntrance);
            zone.JoinQueue(visitor.gameObject);
            Set(visitor, "targetZone", zone);
            visitor.SetState(ClientState.Leaving);
            Require(!zone.waitingQueue.Contains(visitor.gameObject), "Departing visitor remained in the local queue.");

            // Exercise the actual nested iterators without starting gameplay or waiting on frames.
            Set(visitor, "currentState", ClientState.GettingTicket);
            Set(visitor, "myQueueNumber", 1); // Already has a ticket: synchronous completion.
            Set(visitor, "actionVersion", 10);
            Set(visitor, "isActionRunning", true);
            var runner = (IEnumerator)typeof(ClientStateMachine).GetMethod("RunCurrentState",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(visitor, new object[] { 10 });
            var stack = new Stack<IEnumerator>();
            stack.Push(runner);
            int budget = 20;
            while (stack.Count > 0 && --budget > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                Require(top.Current is IEnumerator, "A synchronous branch unexpectedly started a detached coroutine or wait.");
                stack.Push((IEnumerator)top.Current);
            }
            Require(stack.Count == 0 && !(bool)Get(visitor, "isActionRunning"), "Completed state kept the action loop busy.");
            Set(visitor, "isActionRunning", true);
            runner = (IEnumerator)typeof(ClientStateMachine).GetMethod("RunCurrentState",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(visitor, new object[] { 10 });
            runner.MoveNext();
            visitor.StopAllActionCoroutines();
            Set(visitor, "isActionRunning", true); // A replacement action has started.
            ((IDisposable)runner).Dispose();
            Require((bool)Get(visitor, "isActionRunning"), "Old action completion cleared its replacement.");
            visitor.StopAllActionCoroutines();
            Set(visitor, "myQueueNumber", -1);
            var queueObject = Child(root, "QueueRules");
            queueObject.SetActive(false);
            var queue = queueObject.AddComponent<ClientQueueManager>();
            client.mainGoal = ClientGoal.AskAndLeave;
            Set(visitor, "currentState", ClientState.AtWaitingArea);
            Require(queue.CanCallForRegistration(client, 1), "Waiting visitor cannot be called.");
            client.mainGoal = ClientGoal.VisitToilet;
            Require(!queue.CanCallForRegistration(client, 1), "Toilet-only visitor was called for registration.");
            client.mainGoal = ClientGoal.AskAndLeave;
            Set(visitor, "currentState", ClientState.AtLimitedZoneEntrance);
            Require(!queue.CanCallForRegistration(client, 1), "Visitor in another room's queue was called.");
            Set(visitor, "currentState", ClientState.WaitingForDocument);
            Require(!queue.CanCallForRegistration(client, 1), "Visitor waiting for a document was called again.");
            Set(client, "_maxPatienceValue", 100f);
            Set(client, "_currentStressValue", 20f);
            Set(visitor, "currentState", ClientState.Grumbling);
            var monitor = (IEnumerator)typeof(ClientStateMachine).GetMethod("GlobalPatienceMonitor",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(visitor, null);
            monitor.MoveNext(); // First timer.
            Require(monitor.MoveNext() && (float)Get(client, "_currentStressValue") > 20f,
                "Grumbling froze stress accumulation.");
            Set(visitor, "currentState", ClientState.Confused);
            float before = (float)Get(client, "_currentStressValue");
            Require(monitor.MoveNext() && (float)Get(client, "_currentStressValue") == before,
                "Temporary confusion stopped the monitor or accumulated stress.");
            Set(visitor, "currentState", ClientState.MovingToSeat);
            Require(monitor.MoveNext() && (float)Get(client, "_currentStressValue") > before,
                "Stress did not resume after confusion.");
            ((IDisposable)monitor).Dispose();

            var desk = Child(root, "ServiceDesk").AddComponent<ServicePoint>();
            desk.clientStandPoint = spot;
            desk.AssignClient(client);
            desk.SetClientReady();
            Set(visitor, "assignedServicePoint", desk);
            Set(visitor, "currentGoal", spot);
            visitor.SetGoal(outside);
            Require(desk.CurrentClient == null && !desk.IsClientPhysicallyReady,
                "Leaving for another destination kept the service desk occupied.");
            var nextClient = second.AddComponent<ClientPathfinding>();
            desk.AssignClient(nextClient);
            Set(visitor, "assignedServicePoint", desk);
            visitor.SetState(ClientState.LeavingUpset);
            Require(desk.CurrentClient == nextClient, "Stale cleanup cleared the next desk client.");
            desk.ClearClient(nextClient);
            zone.ReleaseWaypoint(spot);
            desk.transform.SetParent(zone.transform);
            var nextVisitor = second.AddComponent<ClientStateMachine>();
            nextClient.stateMachine = nextVisitor;
            Set(nextVisitor, "parent", nextClient);
            Set(nextVisitor, "feedbackController", second.GetComponent<ClientFeedbackController>());
            Set(nextVisitor, "actionDiary", second.GetComponent<ActionDiary>());
            Set(visitor, "currentState", ClientState.AtWaitingArea);
            var provider = new CheckProvider(desk);
            Require(visitor.TryGetCalledToSpecificDesk(spot, 2, provider), "First ticket could not reserve the desk.");
            Require(!nextVisitor.TryGetCalledToSpecificDesk(spot, 5, provider), "Second ticket replaced the pending first visitor.");
            Require(!zone.TryOccupyWaypoint(spot, second) && desk.CurrentClient == client,
                "Walk-in stole a called visitor's place.");
            Set(client, "_currentStressValue", 90f);
            client.canGrumble = true;
            monitor = (IEnumerator)typeof(ClientStateMachine).GetMethod("GlobalPatienceMonitor",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(visitor, null);
            monitor.MoveNext();
            monitor.MoveNext();
            Require(visitor.GetCurrentState() == ClientState.MovingToGoal, "Grumbling interrupted an accepted service appointment.");
            ((IDisposable)monitor).Dispose();
            visitor.SetState(ClientState.LeavingUpset);
            Require(nextVisitor.TryGetCalledToSpecificDesk(spot, 5, provider), "Desk stayed blocked after the first visitor left.");
            nextVisitor.SetState(ClientState.LeavingUpset);
            visitor.GetComponent<ClientFeedbackController>().Initialize(client);
            Set(visitor, "currentState", ClientState.MovingToGoal);
            queue.queue[client] = 2;
            queue.currentlyCalledNumbers.Add(2);
            queue.clientsAwaitingResponse[2] = Time.time - queue.clientResponseTimeout - 1f;
            typeof(ClientQueueManager).GetMethod("HandleTimedOutClients", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(queue, null);
            Require(queue.queue.ContainsKey(client) && !queue.currentlyCalledNumbers.Contains(2) && !queue.clientsAwaitingResponse.ContainsKey(2),
                "Timeout lost the visitor ticket or kept the expired call active.");
            Set(visitor, "currentState", ClientState.AtWaitingArea);
            Require(queue.CanCallForRegistration(client, 2), "Visitor cannot be called again after timeout recovery.");
            return "PASS: client lifecycle, exclusive desk admission for tickets 2/5, walk-in conflict, appointment stress, successor admission, timeout retains ticket and permits recall.";
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static GameObject Child(GameObject parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform);
        return child;
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

    private static object Get(object target, string field) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class CheckProvider : IServiceProvider
    {
        private readonly ServicePoint desk;
        public CheckProvider(ServicePoint desk) => this.desk = desk;
        public bool IsAvailableToServe => true;
        public Transform GetClientStandPoint() => desk.clientStandPoint.transform;
        public ServicePoint GetWorkstation() => desk;
        public void AssignClient(ClientPathfinding client) { }
    }
}
