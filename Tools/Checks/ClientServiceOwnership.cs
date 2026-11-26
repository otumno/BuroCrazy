using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using Managers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ClientServiceOwnershipCheck
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("ServiceOwnershipCheck");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var desk = Child(root, "Desk").AddComponent<ServicePoint>();
            desk.clientStandPoint = Child(root, "VisitorPoint").AddComponent<Waypoint>();
            var otherDesk = Child(root, "NeighbourDesk").AddComponent<ServicePoint>();
            otherDesk.clientStandPoint = Child(root, "NeighbourPoint").AddComponent<Waypoint>();
            var worker = Child(root, "Worker").AddComponent<ClerkController>();
            worker.assignedWorkstation = desk;
            var executor = worker.gameObject.AddComponent<ServiceAtCashierExecutor>();
            Set(executor, typeof(ActionExecutor), "staff", worker);
            var brain = Child(root, "Visitor").AddComponent<ClientStateMachine>();
            var client = brain.GetComponent<ClientPathfinding>();
            client.stateMachine = brain;
            Set(brain, typeof(ClientStateMachine), "parent", client);
            Set(brain, typeof(ClientStateMachine), "currentState", ClientState.AtCashier);
            Set(brain, typeof(ClientStateMachine), "currentGoal", desk.clientStandPoint);
            Set(brain, typeof(ClientStateMachine), "assignedServicePoint", desk);
            root.SetActive(true);
            Require(CanServe(executor, client), "Own arrived visitor rejected.");
            Set(brain, typeof(ClientStateMachine), "assignedServicePoint", otherDesk);
            Require(!CanServe(executor, client), "Neighbour visitor accepted at same physical position.");
            Set(brain, typeof(ClientStateMachine), "assignedServicePoint", null);
            Set(brain, typeof(ClientStateMachine), "currentGoal", otherDesk.clientStandPoint);
            Require(!CanServe(executor, client), "Unassigned passer-by accepted.");
            Set(brain, typeof(ClientStateMachine), "currentGoal", desk.clientStandPoint);
            Require(CanServe(executor, client), "Legacy own-goal visitor rejected.");
            client.transform.position = Vector3.right * 5;
            Require(!CanServe(executor, client), "Remote visitor accepted.");
            client.transform.position = Vector3.zero;
            var wait = (IEnumerator)typeof(ActionExecutor).GetMethod("WaitForClientService", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(executor, new object[] { client, 100f });
            Require(wait.MoveNext(), "Valid service wait ended immediately.");
            Set(brain, typeof(ClientStateMachine), "currentState", ClientState.Enraged);
            Require(!wait.MoveNext() && !CanServe(executor, client), "Angry visitor did not cancel service wait.");
            Set(brain, typeof(ClientStateMachine), "currentState", ClientState.AtCashier);
            var otherClient = Child(root, "OtherClient").AddComponent<ClientPathfinding>();
            desk.TryAssignClient(otherClient);
            Require(!CanServe(executor, client), "Occupied desk accepted another client.");
            desk.ClearClient(otherClient);
            desk.clientStandPoint = null;
            Require(!CanServe(executor, client), "Missing stand point accepted.");

            CheckReassignedReturn(worker, executor, desk, otherDesk);
            desk.clientStandPoint = Child(root, "FirstWindowPoint").AddComponent<Waypoint>();
            var neighbour = Child(root, "SecondRegistrar").AddComponent<ClerkController>();
            desk.deskId = otherDesk.deskId = 0;
            worker.assignedWorkstation = desk;
            neighbour.assignedWorkstation = otherDesk;
            try
            {
                ClientSpawner.AssignServiceProviderToDesk(worker, desk);
                ClientSpawner.AssignServiceProviderToDesk(neighbour, otherDesk);
                Require(ReferenceEquals(ClientSpawner.GetServiceProviderAtDesk(desk), worker) &&
                    ReferenceEquals(ClientSpawner.GetServiceProviderAtDesk(otherDesk), neighbour), "One registration window displaced the other.");
                ClientSpawner.UnassignServiceProviderFromDesk(desk, worker);
                Require(ClientSpawner.GetServiceProviderAtDesk(desk) == null &&
                    ReferenceEquals(ClientSpawner.GetServiceProviderAtDesk(otherDesk), neighbour), "Departing registrar closed both windows.");
                var zone = root.AddComponent<LimitedCapacityZone>();
                zone.capacity = 2;
                zone.insideWaypoints = new System.Collections.Generic.List<Waypoint> { desk.clientStandPoint, otherDesk.clientStandPoint };
                var spawner = Child(root, "Spawner").AddComponent<ClientSpawner>();
                spawner.registrationZone = zone;
                var previousSpawner = ClientSpawner.Instance;
                try
                {
                    typeof(ClientSpawner).GetProperty("Instance").SetValue(null, spawner);
                    neighbour.SetState(ClerkController.ClerkState.Working);
                    var visitorActions = Child(root, "Admit").AddComponent<ClientActionExecutor>();
                    var reserve = typeof(ClientActionExecutor).GetMethod("ReserveVisitorPoint", BindingFlags.Instance | BindingFlags.NonPublic);
                    Require((Waypoint)reserve.Invoke(visitorActions, new object[] { zone, client.gameObject }) == otherDesk.clientStandPoint,
                        "Visitor chose the unstaffed registration window.");
                    Require((Waypoint)reserve.Invoke(visitorActions, new object[] { zone, otherClient.gameObject }) == null,
                        "Unstaffed window admitted another visitor.");
                }
                finally { typeof(ClientSpawner).GetProperty("Instance").SetValue(null, previousSpawner); }

                var registry = Child(root, "Registry").AddComponent<ScenePointsRegistry>();
                var assignments = Child(root, "Assignments").AddComponent<AssignmentManager>();
                registry.allServicePoints = new System.Collections.Generic.List<ServicePoint> { desk, otherDesk };
                var saved = new StaffSaveData { role = StaffController.Role.Registrar, assignedWorkstationId = 0 };
                Require(SaveLoadManager.ResolveSavedWorkstation(saved, registry, assignments) == desk, "Legacy first window lost.");
                assignments.AssignStaffToWorkstation(worker, desk);
                Require(SaveLoadManager.ResolveSavedWorkstation(saved, registry, assignments) == otherDesk, "Legacy second window lost.");
                var path = typeof(SaveLoadManager).GetMethod("GetHierarchyPath", BindingFlags.Static | BindingFlags.NonPublic);
                saved.assignedWorkstationPath = (string)path.Invoke(null, new object[] { otherDesk.transform });
                assignments.UnassignStaff(worker);
                var copy = JsonUtility.FromJson<StaffSaveData>(JsonUtility.ToJson(saved));
                Require(SaveLoadManager.ResolveSavedWorkstation(copy, registry, assignments) == otherDesk, "Saved physical window lost.");
            }
            finally
            {
                ClientSpawner.UnassignServiceProviderFromDesk(desk, worker);
                ClientSpawner.UnassignServiceProviderFromDesk(otherDesk, neighbour);
            }

            // Exercise mover completion directly, without editor coroutines or Play Mode.
            var paper = Child(root, "Paper").AddComponent<DocumentMover>();
            var target = Child(root, "Hand").transform;
            int arrivals = 0;
            Set(paper, typeof(DocumentMover), "target", target);
            Set(paper, typeof(DocumentMover), "onArrival", (Action)(() => arrivals++));
            var move = (IEnumerator)typeof(DocumentMover).GetMethod("MoveToTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(paper, null);
            Require(!move.MoveNext() && arrivals == 1, "Arrival callback missing.");
            Set(paper, typeof(DocumentMover), "target", target);
            Set(paper, typeof(DocumentMover), "onArrival", (Action)(() => arrivals++));
            UnityEngine.Object.DestroyImmediate(target.gameObject);
            move = (IEnumerator)typeof(DocumentMover).GetMethod("MoveToTarget", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(paper, null);
            Require(!move.MoveNext() && arrivals == 1, "Destroyed hand falsely completed delivery.");
            // Avoid game-completion/destruction effects while disposing this editor-only fixture.
            Set(executor, typeof(ActionExecutor), "_finished", true);
            return "PASS: separate same-ID registration windows, staffed-only visitor admission, modern and legacy save restoration; own/neighbor/passer-by/distant/angry/occupied/missing-point guards; service wait cancellation, reassigned and removed desk on return; document arrival versus lost target.";
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void CheckReassignedReturn(ClerkController worker, ActionExecutor executor, ServicePoint desk, ServicePoint otherDesk)
    {
        desk.clerkStandPoint = desk.transform;
        otherDesk.clerkStandPoint = otherDesk.transform;
        otherDesk.transform.position = Vector3.right * 8f;
        desk.deskId = -8001;
        otherDesk.deskId = -8002;
        var returnMethod = typeof(ActionExecutor).GetMethod("ReturnToWorkstation", BindingFlags.Instance | BindingFlags.NonPublic);
        var route = (IEnumerator)returnMethod.Invoke(executor, null);
        try
        {
            Require(route.MoveNext(), "Worker did not set out for the first desk.");
            worker.assignedWorkstation = otherDesk;
            Require(route.MoveNext() && Managers.ClientSpawner.GetServiceProviderAtDesk(otherDesk.deskId) == null,
                "Worker registered at the new desk before reaching it.");
            worker.transform.position = otherDesk.clerkStandPoint.position;
            Require(!route.MoveNext() && ReferenceEquals(Managers.ClientSpawner.GetServiceProviderAtDesk(otherDesk.deskId), worker),
                "Worker did not take over the new desk on arrival.");
            route = (IEnumerator)returnMethod.Invoke(executor, null);
            Require(route.MoveNext(), "Missing return before unassignment.");
            worker.assignedWorkstation = null;
            Require(!route.MoveNext(), "Unassigned worker continued the return.");
        }
        finally
        {
            Managers.ClientSpawner.UnassignServiceProviderFromDesk(otherDesk, worker);
        }
    }

    private static bool CanServe(ActionExecutor executor, ClientPathfinding client) =>
        (bool)typeof(ActionExecutor).GetMethod("CanServeAtWorkstation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(executor, new object[] { client });
    private static void Set(object target, Type type, string field, object value) =>
        type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static GameObject Child(GameObject parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform);
        return child;
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
