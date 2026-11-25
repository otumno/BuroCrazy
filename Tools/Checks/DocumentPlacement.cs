using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DocumentPlacementCheck
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var random = UnityEngine.Random.state;
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            int checkedDesks = 0;
            foreach (var desk in roots.SelectMany(r => r.GetComponentsInChildren<ServicePoint>(true)).Where(d => d.documentStack != null))
            {
                var stack = desk.documentStack;
                var expected = stack.stackRoot != null ? stack.stackRoot : desk.documentPointOnDesk != null ? desk.documentPointOnDesk : stack.transform;
                Require(stack.EffectiveStackRoot == expected, "Wrong anchor: " + desk.name);
                int before = stack.CurrentSize;
                Require(stack.AddDocumentToStack(), "Could not create report at " + desk.name);
                var paper = stack.GetDocumentsForSave().Last();
                Require(Vector3.Distance(paper.transform.position, expected.position + Vector3.up * before * stack.stackOffset) < .001f,
                    "Paper not on desk surface: " + desk.name);
                Require(paper.GetComponentsInChildren<SpriteRenderer>().All(r => r.sortingOrder >= 50), "Paper sorting order missing.");
                checkedDesks++;
            }
            var archive = roots.SelectMany(r => r.GetComponentsInChildren<Managers.ArchiveManager>(true)).Single();
            Require(!archive.OwnsStack(null), "Null treated as archive stack.");
            var source = archive.mainDocumentStack;
            var destination = archive.GetDropOffStack(archive.overflowPoints.First(p => p != null));
            int sourceBefore = source.CurrentSize;
            int destinationBefore = destination.CurrentSize;
            using (var transfer = new DocumentStackTransfer(source, 1))
            {
                Require(transfer.Count == 1, "No paper picked up.");
                Require(source.GetDocumentsForSave().Count == sourceBefore, "In-transit paper lost to saves.");
                Require(transfer.DeliverTo(destination) == 1, "Overflow delivery failed.");
            }
            Require(destination.CurrentSize == destinationBefore + 1 && source.CurrentSize == sourceBefore - 1, "Delivered paper count wrong.");
            using (var transfer = new DocumentStackTransfer(destination, 1)) { }
            Require(destination.CurrentSize == destinationBefore + 1, "Cancellation lost a paper.");
            return "PASS: " + checkedDesks + " actual desk surfaces, report creation/sorting, overflow delivery, in-transit save inclusion and rollback.";
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Random.state = random;
        }
    }
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
