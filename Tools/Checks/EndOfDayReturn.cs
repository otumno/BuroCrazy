using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Managers;
using UI.EndOfDay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run outside Play Mode. Does not save the gameplay scene or prefab.
public static class EndOfDayReturnCheck
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        CheckSceneReferences();
        var scene = EditorSceneManager.NewPreviewScene();
        DocumentStackTransfer transfer = null;
        try
        {
            var directorObject = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Director.prefab"), scene);
            var director = directorObject.GetComponent<DirectorAvatarController>();
            director.agentMover = directorObject.GetComponent<AgentMover>();
            director.visuals = directorObject.GetComponent<Characters.CharacterVisuals>();
            var chair = Make("Chair", scene);
            chair.transform.position = new Vector3(7, 3, 0);
            director.directorChairPoint = chair.transform;
            var source = Make("Source", scene).AddComponent<DocumentStack>();
            source.maxStackSize = 1;
            var paper = Make("OriginalPaper", scene);
            Require(source.ReceiveExistingDocument(paper), "Cannot seed source.");
            transfer = new DocumentStackTransfer(source, 1);
            Require(source.ReceiveExistingDocument(Make("NewPaper", scene)), "Cannot refill source.");
            Set(director, "documentTransfer", transfer);
            var archive = Make("Archive", scene).AddComponent<ArchiveManager>();
            var overflow = Make("Overflow", scene).transform;
            archive.overflowPoints = new List<Transform> { overflow };
            var delivery = transfer.DeliverToArchive(archive, director.transform, position => null);
            Require(delivery.MoveNext(), "Director failed to start the trip to overflow.");
            director.SetState(DirectorAvatarController.DirectorState.CarryingDocuments);
            director.SetUninterruptible(true);
            var body = directorObject.GetComponent<Rigidbody2D>();
            body.linearVelocity = new Vector2(2, 1);
            director.ReturnToDeskAfterDayEnd();
            Require(director.IsAtDesk && director.GetCurrentState() == DirectorAvatarController.DirectorState.AtDesk,
                "Director retained previous action state.");
            Require(!director.IsInUninterruptibleAction, "Cancelled action still blocks director input.");
            Require(Vector2.Distance(body.position, chair.transform.position) < .001f && body.linearVelocity == Vector2.zero,
                "Teleport left old position or momentum.");
            Require(transfer.Count == 0 && source.CurrentSize == 2 && source.GetDocumentsForSave().Contains(paper),
                "Night cancellation lost the original paper or failed with a full source.");
            Require(paper.activeSelf && paper.transform.parent == source.transform, "Returned paper is not visible at source.");
            Require(archive.RequestDropOffPoint() == overflow, "Night cancellation leaked the overflow reservation.");
            archive.FreeOverflowPoint(overflow);
            director.ReturnToDeskAfterDayEnd();
            Require(source.CurrentSize == 2, "Repeated return duplicated papers.");

            return "PASS: scene sequence/buttons/calendar, director state and physics, document rollback into full source, overflow release, repeated return, statistics directly followed by an order.";
        }
        finally
        {
            transfer?.Dispose();
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void CheckSceneReferences()
    {
        string path = AssetDatabase.FindAssets("GameScene t:Scene").Select(AssetDatabase.GUIDToAssetPath)
            .Single(p => p.EndsWith("/GameScene.unity"));
        var scene = EditorSceneManager.OpenPreviewScene(path);
        var originalLocale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
        try
        {
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale("en");
            var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var sequence = components.OfType<EndOfDaySequence>().Single();
            Require(sequence.gameObject.activeInHierarchy && sequence.enabled, "End-of-day sequence disabled.");
            var serialized = new SerializedObject(sequence);
            foreach (var name in new[] { "bookkeepingPanel", "orderSelection" })
                Require(serialized.FindProperty(name).objectReferenceValue != null, "Missing sequence reference: " + name);
            Require(serialized.FindProperty("schedulePanel") == null, "Night sequence still depends on the schedule.");
            var panelData = new SerializedObject(serialized.FindProperty("bookkeepingPanel").objectReferenceValue);
            var button = (UnityEngine.UI.Button)panelData.FindProperty("nextButton").objectReferenceValue;
            Require(button != null, "Missing Next button on statistics.");
            var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
            var binding = label.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
            Require(binding != null && binding.StringReference.TableEntryReference.Key == "ui.common.next" &&
                binding.OnUpdateString.GetPersistentTarget(0) == label, "Next button label binding invalid.");
            var orders = components.OfType<OrderManager>().Single();
            var card = components.OfType<OrderCardUI>().First();
            var daily = orders.allPossibleOrders.Where(o => o != null && o.duration == OrderDuration.SingleDay).ToArray();
            Require(daily.Length >= 2, "Daily choice missing.");
            orders.SelectOrder(daily[0]);
            card.Setup(daily[1], orders.SelectOrderForNextDay);
            var cardData = new SerializedObject(card);
            var description = (TMPro.TMP_Text)cardData.FindProperty("descriptionText").objectReferenceValue;
            Require(description != null && !string.IsNullOrEmpty(description.text), "Missing order description: " + card.name + " / " + daily[1].name);
            Require(description.text.Contains(daily[1].allowedDirectorErrorRate.ToString("0.#%")), "Daily card hides the allowed error rate.");
            var select = (UnityEngine.UI.Button)cardData.FindProperty("selectButton").objectReferenceValue;
            select.onClick.Invoke();
            Require(orders.PendingOrder == daily[1] && orders.AllowedDirectorErrorRate == daily[0].allowedDirectorErrorRate,
                "Evening choice replaced the outgoing day.");
            orders.RestoreSelection(orders.activeOrders.ToArray(), orders.PendingOrder, true);
            orders.BeginNextDay();
            Require(orders.PendingOrder == null && orders.HasSelectedOrderForDay && orders.activeOrders.Count(o => o == daily[1]) == 1 &&
                !orders.activeOrders.Contains(daily[0]) && orders.AllowedDirectorErrorRate == daily[1].allowedDirectorErrorRate,
                "Restored evening order failed to take effect on the next day.");
            var clock = components.OfType<TimeManager>().Single();
            var periods = clock.mainCalendarDay.periodSettings;
            Require(periods[0].PeriodType == Data.Calendar.CalendarDayPeriodType.EndNight &&
                    periods[periods.Count - 1].PeriodType == Data.Calendar.CalendarDayPeriodType.StartNight,
                "Calendar wrap is not between night halves.");
            Require(components.OfType<DirectorAvatarController>().Single().directorChairPoint != null, "Director desk return point missing.");
        }
        finally { UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = originalLocale; EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static GameObject Make(string name, Scene scene)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }
    private static void Set(object value, string name, object data) => value.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(value, data);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
