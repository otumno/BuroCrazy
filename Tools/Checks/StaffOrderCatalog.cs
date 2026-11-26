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
        var previousArchive = Managers.ArchiveManager.Instance;
        var archiveProperty = typeof(Managers.ArchiveManager).GetProperty("Instance");
        try
        {
            var archive = Make("Archive", scene).AddComponent<Managers.ArchiveManager>();
            archiveProperty.SetValue(null, archive);
            var catalog = AssetDatabase.LoadAssetAtPath<StaffOrderCatalog>("Assets/Data/StaffOrders/StaffOrderCatalog.asset");
            Require(catalog != null && catalog.Orders.All(o => o.Action != null && o.Action.GetExecutorType() != null), "Invalid catalog action.");
            foreach (var key in new[] { "order.break", "order.toilet", "order.guard_patrol", "order.archive", "order.clean", "order.intern_patrol" })
                Require(catalog.Orders.Count(o => o.LabelKey == key) == 1, "Missing or duplicate order key: " + key);
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

            archive.mainDocumentStack = stack;
            Require(!archiveOrder.Action.CanBeOrdered(clerk), "Archive stack can be ordered to deliver to itself.");
            var order = catalog.CreateOrder(archiveOrder);
            order.Start(100);
            Require(order.GetPriorityBonus(100) == catalog.InitialPriority, "Initial priority mismatch.");
            Require(Mathf.Approximately(order.GetPriorityBonus(100 + order.DecaySeconds / 2), order.InitialPriority / 2), "Priority does not decay.");
            Require(order.IsExpired(100 + order.DecaySeconds), "Order does not expire.");
            CheckMenuInScene();
            return "PASS: scene menu/header references, common and role orders, ordinary vs project papers, linear priority decay and expiry. HUD inserts, sorts, removes and recolors real rows; order replacement is tested by WorkActionAssets.";
        }
        finally
        {
            archiveProperty.SetValue(null, previousArchive);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void CheckMenuInScene()
    {
        var path = AssetDatabase.FindAssets("GameScene t:Scene").Select(AssetDatabase.GUIDToAssetPath).Single(p => p.EndsWith("/GameScene.unity"));
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            CheckHud(scene);
            var menu = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UI.StaffOrders.StaffOrderMenuUI>(true)).Single();
            var data = new SerializedObject(menu);
            foreach (var field in new[] { "catalog", "portrait", "nameText", "roleText", "panel", "itemsContainer", "itemPrefab", "backgroundButton" })
                Require(data.FindProperty(field).objectReferenceValue != null, "Menu missing " + field);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void CheckHud(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var hud = roots.SelectMany(r => r.GetComponentsInChildren<UI.StaffShiftHUD>(true)).Single();
        var hiring = roots.SelectMany(r => r.GetComponentsInChildren<Managers.HiringManager>(true)).Single();
        var oldHiring = Managers.HiringManager.Instance;
        var oldLocale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        try
        {
            Managers.HiringManager.Instance = hiring;
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale("ru");
            hiring.AllStaff.Clear();
            var first = Make("First arrival", scene).AddComponent<StaffController>();
            var second = Make("Second arrival", scene).AddComponent<StaffController>();
            var sequence = typeof(StaffController).GetField("<ShiftSequence>k__BackingField", flags);
            sequence.SetValue(first, 3L);
            sequence.SetValue(second, 4L);
            first.currentRole = StaffController.Role.Guard;
            second.currentRole = StaffController.Role.Intern;
            first.hasLeftToday = second.hasLeftToday = false;
            first.bladder = 75;
            first.stress = 32;
            hiring.AllStaff.Add(second);
            hiring.AllStaff.Add(first);
            void Refresh()
            {
                typeof(UI.StaffShiftHUD).GetField("nextRefresh", flags).SetValue(hud, 0f);
                typeof(UI.StaffShiftHUD).GetMethod("Update", flags).Invoke(hud, null);
            }
            Refresh();
            var rows = (System.Collections.Generic.Dictionary<StaffController, UI.StaffShiftRowUI>)
                typeof(UI.StaffShiftHUD).GetField("rows", flags).GetValue(hud);
            Require(rows.Count == 2 && rows[first].transform.GetSiblingIndex() < rows[second].transform.GetSiblingIndex(), "HUD used hiring order instead of arrival order.");
            var data = new SerializedObject(rows[first]);
            var frame = (UnityEngine.UI.Image)data.FindProperty("portraitFrame").objectReferenceValue;
            var db = (RoleColorDatabase)data.FindProperty("roleColors").objectReferenceValue;
            Require(db != null && frame.color == db.GetColorForRole(first.currentRole, Color.gray), "HUD frame does not use role database.");
            Require(Mathf.Approximately(((RectTransform)data.FindProperty("bladderFill").objectReferenceValue).anchorMax.x, .75f) &&
                Mathf.Approximately(((RectTransform)data.FindProperty("stressFill").objectReferenceValue).anchorMax.x, .32f), "HUD bars do not reflect staff needs.");
            first.BeginNeedActivity(false);
            Refresh();
            Require(rows.ContainsKey(first), "Break removed an active employee.");
            first.hasLeftToday = true;
            // Edit Mode cannot enqueue Destroy. Remove the isolated view, then check the real roster reconciliation.
            UnityEngine.Object.DestroyImmediate(rows[first].gameObject);
            Refresh();
            Require(!rows.ContainsKey(first) && rows.Count == 1, "Off-duty employee still in HUD.");
            first.hasLeftToday = false;
            sequence.SetValue(first, 5L);
            Refresh();
            Require(rows[second].transform.GetSiblingIndex() < rows[first].transform.GetSiblingIndex(), "Returning employee did not join at bottom.");
        }
        finally
        {
            Managers.HiringManager.Instance = oldHiring;
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = oldLocale;
        }
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
