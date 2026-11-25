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
        var planningWasOpen = ShiftPlanning.IsOpen;
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
            Require(archive.RequestDropOffPoint() == overflow, "Cannot reserve overflow.");
            Set(director, "deliveryArchive", archive);
            Set(director, "deliveryPoint", overflow);
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

            var row = Make("ScheduleRow", scene).AddComponent<StaffScheduleRowUI>();
            Set(row, "_staff", director);
            Set(row, "_isEditable", true);
            director.WorkShiftMask = Data.Calendar.CalendarDayPeriodType.Morning;
            ShiftPlanning.Close();
            row.StartShiftAnimation(null);
            Require(director.WorkShiftMask == Data.Calendar.CalendarDayPeriodType.Morning, "Stale editable row changed a daytime shift.");
            return "PASS: scene sequence/buttons/calendar, director state and physics, document rollback into full source, overflow release, repeated return and daytime lock.";
        }
        finally
        {
            transfer?.Dispose();
            EditorSceneManager.ClosePreviewScene(scene);
            if (planningWasOpen) ShiftPlanning.Open(); else ShiftPlanning.Close();
        }
    }

    private static void CheckSceneReferences()
    {
        string path = AssetDatabase.FindAssets("GameScene t:Scene").Select(AssetDatabase.GUIDToAssetPath)
            .Single(p => p.EndsWith("/GameScene.unity"));
        var scene = EditorSceneManager.OpenPreviewScene(path);
        try
        {
            var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var sequence = components.OfType<EndOfDaySequence>().Single();
            Require(sequence.gameObject.activeInHierarchy && sequence.enabled, "End-of-day sequence disabled.");
            var serialized = new SerializedObject(sequence);
            foreach (var name in new[] { "bookkeepingPanel", "schedulePanel", "orderSelection" })
                Require(serialized.FindProperty(name).objectReferenceValue != null, "Missing sequence reference: " + name);
            foreach (var name in new[] { "bookkeepingPanel", "schedulePanel" })
            {
                var panel = serialized.FindProperty(name).objectReferenceValue;
                var panelData = new SerializedObject(panel);
                var button = (UnityEngine.UI.Button)panelData.FindProperty("nextButton").objectReferenceValue;
                if (name == "schedulePanel" && button == null)
                    button = (UnityEngine.UI.Button)panelData.FindProperty("closeButton").objectReferenceValue;
                Require(button != null, "Missing Next button: " + name);
                if (name == "bookkeepingPanel")
                    Require(button.GetComponentInChildren<TMPro.TMP_Text>(true).text.Contains("Далее"), "Next button label: " + name);
            }
            var clock = components.OfType<TimeManager>().Single();
            var periods = clock.mainCalendarDay.periodSettings;
            Require(periods[0].PeriodType == Data.Calendar.CalendarDayPeriodType.EndNight &&
                    periods[periods.Count - 1].PeriodType == Data.Calendar.CalendarDayPeriodType.StartNight,
                "Calendar wrap is not between night halves.");
            Require(components.OfType<DirectorAvatarController>().Single().directorChairPoint != null, "Director desk return point missing.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
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
