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
            CheckClientPapers(scene, roots.SelectMany(r => r.GetComponentsInChildren<ServicePoint>(true))
                .First(d => d.documentPointOnDesk != null).documentPointOnDesk);
            var archive = roots.SelectMany(r => r.GetComponentsInChildren<Managers.ArchiveManager>(true)).Single();
            Require(!archive.OwnsStack(null), "Null treated as archive stack.");
            var desks = roots.SelectMany(r => r.GetComponentsInChildren<ServicePoint>(true))
                .Select(d => d.documentStack).Where(s => s != null && !archive.OwnsStack(s)).Distinct().Take(2).ToArray();
            CheckConcurrentDelivery(archive, desks[0], desks[1]);
            return "PASS: " + checkedDesks + " desk surfaces, both client forms, concurrent couriers, partial drop-offs, overflow reservation release, in-transit save inclusion and rollback.";
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Random.state = random;
        }
    }
    private static void CheckConcurrentDelivery(Managers.ArchiveManager archive, DocumentStack firstDesk, DocumentStack secondDesk)
    {
        var main = archive.mainDocumentStack;
        int before = main.CurrentSize;
        archive.maxCapacityBeforeOverflow = before + 1;
        var overflow = archive.GetDropOffStack(archive.overflowPoints[0]);
        overflow.maxStackSize = 2;
        Require(firstDesk.AddDocumentToStack() && firstDesk.AddDocumentToStack(), "Cannot seed three papers.");
        var originals = firstDesk.GetDocumentsForSave().ToArray();
        var secondOriginal = secondDesk.GetDocumentsForSave().Single();
        var firstCarrier = new GameObject("FirstCourier").transform;
        var secondCarrier = new GameObject("SecondCourier").transform;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(firstCarrier.gameObject, main.gameObject.scene);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(secondCarrier.gameObject, main.gameObject.scene);
        var mover = firstCarrier.gameObject.AddComponent<AgentMover>();
        firstCarrier.position = overflow.transform.position + Vector3.right * (1f + mover.stoppingDistance * .5f);
        Require(Managers.ArchiveManager.IsWithinReach(firstCarrier, overflow.transform.position), "Rejected a valid waypoint stop.");
        firstCarrier.position += Vector3.right;
        Require(!Managers.ArchiveManager.IsWithinReach(firstCarrier, overflow.transform.position), "Allowed remote unloading.");
        using (var first = new DocumentStackTransfer(firstDesk, 3))
        using (var second = new DocumentStackTransfer(secondDesk, 1))
        {
            var routeA = first.DeliverToArchive(archive, firstCarrier, point => { firstCarrier.position = point; return null; });
            var routeB = second.DeliverToArchive(archive, secondCarrier, point => { secondCarrier.position = point; return null; });
            Require(routeA.MoveNext() && routeB.MoveNext() && firstCarrier.position == main.transform.position &&
                secondCarrier.position == main.transform.position, "Couriers did not both set out for the main stack.");
            Require(firstDesk.GetDocumentsForSave().Count == 3, "Save missed papers while walking.");
            Require(routeA.MoveNext() && first.Count == 2 && main.CurrentSize == before + 1 &&
                firstCarrier.position == overflow.transform.position, "First courier did not continue to overflow.");
            Require(routeB.MoveNext() && second.Count == 1 && secondCarrier.position == archive.overflowPoints[1].position,
                "Second courier exceeded the main limit or reused reserved overflow.");
            second.Dispose();
            Require(!routeB.MoveNext() && secondDesk.GetDocumentsForSave().Single() == secondOriginal,
                "Cancellation did not restore exactly the second courier's paper.");
            var freed = archive.RequestDropOffPoint();
            Require(freed == archive.overflowPoints[1], "Cancellation leaked the overflow reservation.");
            archive.FreeOverflowPoint(freed);
            Require(!routeA.MoveNext() && first.Count == 0 && overflow.CurrentSize == 2, "Remainder did not arrive.");
            Require(firstDesk.GetDocumentsForSave().Count == 0 && originals.All(paper =>
                main.GetDocumentsForSave().Concat(overflow.GetDocumentsForSave()).Count(p => p == paper) == 1),
                "Completed route lost or duplicated an original paper.");
        }
    }

    private static void CheckClientPapers(UnityEngine.SceneManagement.Scene scene, Transform desk)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Client.prefab");
        var client = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, scene);
        var holder = client.GetComponent<DocumentHolder>();
        Require(holder.handPoint != null, "Client has no hand.");
        foreach (var form in new[] { DocumentType.Form1, DocumentType.Form2 })
        {
            var certificate = form == DocumentType.Form1 ? DocumentType.Certificate1 : DocumentType.Certificate2;
            holder.SetDocument(form);
            var incoming = holder.DetachDocument();
            Require(incoming != null && incoming.transform.parent == null, "Borrowed form still attached.");
            CheckFlight(incoming, holder.handPoint, desk);
            var outgoing = UnityEngine.Object.Instantiate(holder.GetPrefabForType(certificate), desk);
            CheckFlight(outgoing, desk, holder.handPoint);
            holder.ReceiveTransferredDocument(certificate, outgoing);
            Require(holder.GetCurrentDocumentType() == certificate && outgoing.transform.parent == holder.handPoint &&
                outgoing.transform.localPosition == Vector3.zero, "Certificate did not return to the hand.");
            holder.DetachDocument();
        }
    }

    private static void CheckFlight(GameObject paper, Transform from, Transform to)
    {
        var renderer = paper.GetComponentInChildren<SpriteRenderer>();
        Require(renderer != null && renderer.sprite != null && renderer.sortingLayerName == "Personas" && renderer.sortingOrder > 3,
            "Paper renderer behind the character.");
        paper.transform.position = from.position;
        var mover = paper.GetComponent<DocumentMover>() ?? paper.AddComponent<DocumentMover>();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        int arrivals = 0;
        typeof(DocumentMover).GetField("target", flags).SetValue(mover, to);
        typeof(DocumentMover).GetField("onArrival", flags).SetValue(mover, (Action)(() => arrivals++));
        var flight = (System.Collections.IEnumerator)typeof(DocumentMover).GetMethod("MoveToTarget", flags).Invoke(mover, null);
        Require(flight.MoveNext() && paper.transform.parent == null && arrivals == 0, "Flight skipped travel.");
        // Set the last sub-step explicitly. Editor ticks do not have the game's deltaTime.
        paper.transform.position = to.position + Vector3.right * .05f;
        Require(!flight.MoveNext() && arrivals == 1 && paper.transform.position == to.position, "Flight ended off target.");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
