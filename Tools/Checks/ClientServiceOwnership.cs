using System;
using System.Collections;
using System.Reflection;
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
            return "PASS: own/neighbor/passer-by/distant/angry/occupied/missing-point guards; service wait cancellation; document arrival versus lost target.";
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
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
