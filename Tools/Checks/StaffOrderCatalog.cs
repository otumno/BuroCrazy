using System;
using System.Linq;
using StaffOrders;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StaffOrderCatalogCheck
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StaffOrderCatalog>("Assets/Data/StaffOrders/StaffOrderCatalog.asset");
            Require(catalog != null && catalog.Orders.All(o => o.Action != null && o.Action.GetExecutorType() != null), "Invalid catalog action.");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Clerk.prefab"), scene);
            var clerk = go.GetComponent<ClerkController>();
            var archiveOrder = catalog.Orders.Single(o => o.Action is TakeStackToArchiveAction);
            foreach (var role in new[] { StaffController.Role.Clerk, StaffController.Role.Registrar, StaffController.Role.Cashier,
                         StaffController.Role.Archivist, StaffController.Role.Accountant })
            {
                clerk.currentRole = role;
                var orders = catalog.GetOrdersFor(clerk);
                Require(orders.Contains(archiveOrder), "Missing archive order for " + role);
                Require(orders.Count(o => o.Roles.Count == 0) >= 2, "Missing common orders for " + role);
            }
            clerk.currentRole = StaffController.Role.Guard;
            Require(!catalog.GetOrdersFor(clerk).Contains(archiveOrder), "Guard got clerk-specific order.");
            Require(catalog.GetOrdersFor(clerk).Any(o => o.Label == "Иди в патруль"), "Missing guard patrol.");
            clerk.currentRole = StaffController.Role.Clerk;
            var desk = Make("Desk", scene).AddComponent<ServicePoint>();
            var stack = desk.gameObject.AddComponent<DocumentStack>();
            desk.documentStack = stack;
            clerk.assignedWorkstation = desk;
            var project = Make("ProjectPaper", scene);
            project.AddComponent<Gameplay.Documents.ProjectDocumentObject>();
            stack.ReceiveExistingDocument(project);
            Require(!archiveOrder.Action.CanBeOrdered(clerk), "Project-only stack can be ordered into archive.");
            stack.ReceiveExistingDocument(Make("OrdinaryPaper", scene));
            Require(archiveOrder.Action.CanBeOrdered(clerk), "Ordinary paper cannot be ordered into archive.");

            var order = catalog.CreateOrder(archiveOrder);
            order.Start(100);
            Require(order.GetPriorityBonus(100) == catalog.InitialPriority, "Initial priority mismatch.");
            Require(Mathf.Approximately(order.GetPriorityBonus(100 + order.DecaySeconds / 2), order.InitialPriority / 2), "Priority does not decay.");
            Require(order.IsExpired(100 + order.DecaySeconds), "Order does not expire.");
            CheckMenuInScene();
            return "PASS: scene menu/header references, common and role orders, ordinary vs project papers, linear priority decay and expiry. Runtime interruption/stress require gameplay check.";
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void CheckMenuInScene()
    {
        var path = AssetDatabase.FindAssets("GameScene t:Scene").Select(AssetDatabase.GUIDToAssetPath).Single(p => p.EndsWith("/GameScene.unity"));
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var menu = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UI.StaffOrders.StaffOrderMenuUI>(true)).Single();
            var data = new SerializedObject(menu);
            foreach (var field in new[] { "catalog", "portrait", "nameText", "roleText", "panel", "itemsContainer", "itemPrefab", "backgroundButton" })
                Require(data.FindProperty(field).objectReferenceValue != null, "Menu missing " + field);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static GameObject Make(string name, Scene scene)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
