using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Characters;
using Managers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorkActionAssetsCheck
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
        var originalLocale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
        var originalRegistry = ScenePointsRegistry.Instance;
        var originalRandom = UnityEngine.Random.state;
        var originalBins = TrashCan.AllTrashCans;
        var errors = new System.Collections.Generic.List<string>();
        void CollectError(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + string.Join("\n", trace.Split('\n').Take(5)));
        }
        Application.logMessageReceived += CollectError;
        try
        {
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale("ru");
            var roots = scene.GetRootGameObjects();
            var hiring = roots.SelectMany(r => r.GetComponentsInChildren<HiringManager>(true)).Single();
            var registry = roots.SelectMany(r => r.GetComponentsInChildren<ScenePointsRegistry>(true)).Single();
            typeof(ScenePointsRegistry).GetProperty("Instance").SetValue(null, registry);
            var fixture = (GameObject)PrefabUtility.InstantiatePrefab(hiring.internPrefab, scene);
            var visuals = fixture.GetComponent<CharacterVisuals>();
            // Edit Mode does not run Awake on prefab instances. It binds the hand and sprite renderers at runtime.
            typeof(AgentMover).GetMethod("Awake", Private).Invoke(fixture.GetComponent<AgentMover>(), null);
            typeof(CharacterVisuals).GetMethod("Awake", Private).Invoke(visuals, null);
            var shared = hiring.internPrefab.GetComponent<StaffPrefabReferences>();
            var icon = shared.processingIconPrefab.GetComponent<ProcessingAnimationUI>();
            Require(icon.targetImage != null && icon.animationFrames.Count > 1 && icon.animationFrames.All(f => f != null)
                && icon.GetComponent<Canvas>() != null && SortingLayer.layers.Any(l => l.name == "UI"), "Invalid processing effect.");
            var effect = ((GameObject)PrefabUtility.InstantiatePrefab(icon.gameObject, scene)).GetComponent<ProcessingAnimationUI>();
            effect.frameSounds = new System.Collections.Generic.List<AudioClip>();
            typeof(ProcessingAnimationUI).GetMethod("Awake", Private).Invoke(effect, null);
            var from = registry.guardReportDesk.clerkStandPoint.position;
            var to = registry.guardReportDesk.transform.position;
            effect.Play(from, to, 2f);
            Require(Vector3.Distance(effect.transform.position, (from + to) / 2 + Vector3.up * effect.verticalOffset) < .001f &&
                effect.canvas.renderMode == RenderMode.WorldSpace && effect.canvas.sortingLayerName == "UI" && effect.canvas.sortingOrder > 0 &&
                !effect.targetImage.raycastTarget, "Processing icon position, input or sorting changed.");
            effect.StopAllCoroutines();
            var frames = (IEnumerator)typeof(ProcessingAnimationUI).GetMethod("AnimationRoutine", Private).Invoke(effect, new object[] { 2f });
            foreach (var frame in effect.animationFrames)
                Require(frames.MoveNext() && effect.targetImage.sprite == frame, "Skipped processing sprite.");
            var hand = visuals.GetAttachPoint(CharacterVisuals.AttachPointType.Hand);
            Require(hand != null, "No hand on hired prefab.");
            var ranks = hiring.rankDatabase.Where(r => r != null).Distinct().ToList();
            var actions = ranks.SelectMany(r => r.unlockedActions).Distinct().ToList();
            foreach (var action in actions)
            {
                // These rank abilities are implemented inside another job (filing, processing or cleaning).
                if (action is CorrectDocumentAction || action is MakeArchiveRequestAction || action is CheckDocumentAction || action is FindValuablesAction)
                {
                    Require(!action.AreConditionsMet(null), "Passive skill entered autonomous selection: " + action);
                    continue;
                }
                Require(action != null && action.GetExecutorType() != null && !action.GetExecutorType().IsAbstract &&
                    typeof(ActionExecutor).IsAssignableFrom(action.GetExecutorType()), "Invalid action executor in rank data: " + action);
            }
            foreach (var role in new[] { StaffController.Role.Intern, StaffController.Role.OfficeManager })
                Require(ranks.Any(r => r.associatedRole == role && r.unlockedActions.Any(a => a is DeliverDocumentsAction)), "No archive delivery for " + role);
            Require(registry.guardReportDesk?.clerkStandPoint != null && registry.guardReportDesk.documentStack?.documentPrefab != null &&
                registry.dumpsterPoint != null && registry.kitchenPoints.Any(p => p != null) && registry.toiletPoints.Any(p => p != null),
                "Missing guard, kitchen, toilet or dumpster anchor.");
            var roleData = hiring.allRoleData.Single(r => r != null && r.roleType == StaffController.Role.Janitor);
            CheckBag(fixture, registry, roleData, hand);
            CheckNeeds(fixture, registry);
            CheckGuardAndOrders(scene, registry);
            Require(errors.Count == 0, string.Join(" | ", errors.Distinct()));
            return $"PASS: {ranks.Count} configured ranks across {ranks.Select(r => r.associatedRole).Distinct().Count()} roles, {actions.Count} assigned actions; processing frames/layer, archive jobs, guard desk, bag delivery and rollback, guard reports, order replacement and stress, break/toilet and return.";
        }
        finally
        {
            Application.logMessageReceived -= CollectError;
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = originalLocale;
            TrashCan.AllTrashCans = originalBins;
            typeof(ScenePointsRegistry).GetProperty("Instance").SetValue(null, originalRegistry);
            UnityEngine.Random.state = originalRandom;
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void CheckBag(GameObject fixture, ScenePointsRegistry registry, RoleData role, Transform hand)
    {
        var worker = fixture.AddComponent<ServiceWorkerController>();
        worker.ForceInitializeBaseComponents(null, fixture.GetComponent<CharacterVisuals>(), null);
        worker.InitializeFromData(role);
        var bag = worker.TrashBagObject;
        Require(bag != null && bag.transform.parent == hand && !bag.activeSelf, "Janitor bag not initialized on the hand.");
        var renderer = bag.GetComponentInChildren<SpriteRenderer>(true);
        Require(renderer != null && renderer.sprite != null && renderer.enabled, "Trash bag has no visible sprite.");
        var bin = new GameObject("Bin").AddComponent<TrashCan>();
        SceneManager.MoveGameObjectToScene(bin.gameObject, fixture.scene);
        bin.transform.position = fixture.transform.position;
        TrashCan.AllTrashCans = new System.Collections.Generic.List<TrashCan> { bin };
        bin.ReturnCollectedTrash(bin.capacity);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            worker.transform.position = bin.transform.position;
            var executor = fixture.AddComponent<EmptyTrashCanExecutor>();
            var routine = Routine(executor, worker);
            Require(routine.MoveNext() && routine.Current is IEnumerator && !bag.activeSelf, "Bag appeared before collection.");
            Require(routine.MoveNext() && routine.Current is WaitForSeconds, "No collection at bin.");
            Require(routine.MoveNext() && routine.Current is IEnumerator && bag.activeSelf && bin.CurrentFillCount == 0, "Bag pickup failed.");
            worker.transform.position = registry.dumpsterPoint.position;
            Require(routine.MoveNext() && routine.Current is WaitForSeconds && bag.activeSelf, "Bag missing at dumpster.");
            if (attempt == 0)
            {
                Release(executor);
                Require(!bag.activeSelf && bin.CurrentFillCount == bin.capacity && worker.GetCurrentState() == ServiceWorkerController.WorkerState.Idle,
                    "Cancelled trash delivery lost its contents, bag or worker state.");
            }
            else
            {
                Require(!routine.MoveNext(), "Trash delivery continued after disposal.");
                Require(!bag.activeSelf && bin.CurrentFillCount == 0 && worker.GetCurrentState() == ServiceWorkerController.WorkerState.Idle,
                    "Completed trash delivery did not dispose of bag and contents.");
            }
        }
    }

    private static void CheckGuardAndOrders(Scene scene, ScenePointsRegistry registry)
    {
        var go = new GameObject("Guard");
        SceneManager.MoveGameObjectToScene(go, scene);
        var guard = go.AddComponent<GuardMovement>();
        guard.hasLeftToday = false;
        guard.currentRole = StaffController.Role.Guard;
        var desk = registry.guardReportDesk;
        var executor = go.AddComponent<WriteReportExecutor>();
        var routine = Routine(executor, guard);
        int before = desk.documentStack.CurrentSize;
        guard.unwrittenReportPoints = 2;
        Require(routine.MoveNext() && routine.Current is IEnumerator, "Guard did not approach report desk.");
        guard.transform.position = desk.clerkStandPoint.position;
        Require(routine.MoveNext() && guard.GetCurrentState() == GuardMovement.GuardState.WritingReport, "Guard did not start report.");
        Require(!routine.MoveNext() && guard.unwrittenReportPoints == 0 && desk.documentStack.CurrentSize == before + 2 &&
            guard.GetCurrentState() == GuardMovement.GuardState.Idle, "Two reports did not reach the guard desk.");
        var catalog = AssetDatabase.LoadAssetAtPath<StaffOrders.StaffOrderCatalog>("Assets/Data/StaffOrders/StaffOrderCatalog.asset");
        var rest = catalog.CreateOrder(catalog.Orders.Single(o => o.LabelKey == "order.break"));
        var toilet = catalog.CreateOrder(catalog.Orders.Single(o => o.LabelKey == "order.toilet"));
        guard.stress = 0;
        guard.ReceiveOrder(rest);
        var previous = guard.currentExecutor;
        Require(guard.ActiveOrder == rest && previous is GoToBreakExecutor && guard.stress == rest.StressCost, "First order not issued.");
        guard.ReceiveOrder(toilet);
        Require(guard.ActiveOrder == toilet && guard.currentExecutor is GoToToiletExecutor &&
            guard.stress == rest.StressCost + toilet.StressCost, "Replacement lost stress or retained the first executor.");
        guard.OnExecutorFinished(previous, rest.Action, false);
        Require(guard.ActiveOrder == toilet, "Late callback erased the new order.");
    }

    private static void CheckNeeds(GameObject fixture, ScenePointsRegistry registry)
    {
        var staff = fixture.GetComponent<ServiceWorkerController>();
        staff.assignedWorkstation = registry.guardReportDesk;
        registry.kitchenPoints = registry.kitchenPoints.Where(p => p != null).Take(1).ToList();
        registry.toiletPoints = registry.toiletPoints.Where(p => p != null).Take(1).ToList();
        foreach (bool toilet in new[] { false, true })
        {
            staff.energy = 0;
            staff.bladder = 90;
            var executor = (ActionExecutor)fixture.AddComponent(toilet ? typeof(GoToToiletExecutor) : typeof(GoToBreakExecutor));
            var routine = Routine(executor, staff);
            Require(routine.MoveNext() && staff.IsOnBreak() && routine.Current is IEnumerator && !string.IsNullOrEmpty(staff.CurrentSubStatus),
                "Missing approach to need point.");
            staff.transform.position = (toilet ? registry.toiletPoints[0] : registry.kitchenPoints[0]).transform.position;
            for (int i = 0; i < (toilet ? 1 : 10); i++)
                Require(routine.MoveNext() && routine.Current is WaitForSeconds && !string.IsNullOrEmpty(staff.CurrentSubStatus), "Missing need activity.");
            Require(routine.MoveNext() && routine.Current is IEnumerator && (toilet ? staff.bladder == 0 : staff.energy == 100), "Need not satisfied.");
            var returning = (IEnumerator)routine.Current;
            Require(returning.MoveNext() && returning.Current is IEnumerator && staff.IsOnBreak() &&
                !string.IsNullOrEmpty(staff.CurrentSubStatus), "Employee became available before returning to the desk.");
            Release(executor);
            Require(!staff.IsOnBreak() && string.IsNullOrEmpty(staff.CurrentSubStatus), "Stale need status after cleanup.");
        }
    }

    private static IEnumerator Routine(ActionExecutor executor, StaffController staff)
    {
        typeof(ActionExecutor).GetField("staff", Private).SetValue(executor, staff);
        return (IEnumerator)executor.GetType().GetMethod("ActionRoutine", Private).Invoke(executor, null);
    }

    private static void Release(ActionExecutor executor)
    {
        typeof(ActionExecutor).GetMethod("ReleaseResources", Private).Invoke(executor, null);
        typeof(ActionExecutor).GetField("_finished", Private).SetValue(executor, true);
    }

    private static void Require(bool result, string message)
    {
        if (!result) throw new InvalidOperationException(message);
    }
}
