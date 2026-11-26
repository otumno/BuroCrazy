using System;
using System.Linq;
using Managers;
using UnityEditor.SceneManagement;
using UnityEngine;
using Utilities;

public static class StaffWaypointRoutes
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var registry = roots.SelectMany(r => r.GetComponentsInChildren<ScenePointsRegistry>(true)).Single();
            var archive = roots.SelectMany(r => r.GetComponentsInChildren<ArchiveManager>(true)).Single();
            var nodes = roots.SelectMany(r => r.GetComponentsInChildren<Waypoint>()).ToArray();
            var destinations = registry.allServicePoints.Where(d => d != null)
                .SelectMany(d => new[] { d.clerkStandPoint, d.internCollectionPoint })
                .Concat(registry.kitchenPoints.Concat(registry.toiletPoints).Select(w => w.transform))
                .Concat(archive.cabinets.Select(c => c.transform)).Concat(archive.overflowPoints)
                .Concat(new[] { archive.mainDocumentStack.transform, registry.dumpsterPoint })
                .Where(t => t != null).Distinct().ToArray();
            var carriers = roots.SelectMany(r => r.GetComponentsInChildren<StaffController>(true))
                .GroupBy(staff => staff.tag).Select(group => group.First().gameObject).ToArray();
            if (carriers.Length == 0) throw new InvalidOperationException("No staff in GameScene.");
            var home = registry.staffHomeZone.transform.position;
            var trips = 0;
            foreach (var carrier in carriers)
                foreach (var destination in destinations)
                {
                    var outward = PathfindingUtility.BuildPathTo(home, destination.position, carrier, nodes);
                    var back = PathfindingUtility.BuildPathTo(destination.position, home, carrier, nodes);
                    if (outward.Count == 0 || back.Count == 0 ||
                        Vector2.Distance(outward.Last().transform.position, destination.position) > 1f ||
                        back.Last() != outward.Peek() || back.Peek() != outward.Last())
                        throw new InvalidOperationException(carrier.tag + " cannot reach and return from " + destination.name);
                    trips += 2;
                }

            var start = nodes.First(w => w.neighbors != null && w.neighbors.Any(n => n != null && n != w));
            var end = start.neighbors.First(n => n != null && n != start);
            var pair = new[] { start, end };
            // Only the preview copy changes. These two cases used to return a one-node path to the unreachable goal.
            start.neighbors.Clear();
            if (PathfindingUtility.BuildPathTo(start.transform.position, end.transform.position, carriers[0], pair).Count != 0)
                throw new InvalidOperationException("Disconnected goal returned as a direct path.");
            start.neighbors.Add(end);
            end.forbiddenTags = new System.Collections.Generic.List<string> { carriers[0].tag };
            if (PathfindingUtility.BuildPathTo(start.transform.position, end.transform.position, carriers[0], pair).Count != 0)
                throw new InvalidOperationException("Forbidden goal returned as a direct path.");
            return "PASS: " + trips + " routes, " + destinations.Length + " work/need/drop-off destinations, " +
                nodes.Length + " scene waypoints; disconnected and forbidden goals rejected.";
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
