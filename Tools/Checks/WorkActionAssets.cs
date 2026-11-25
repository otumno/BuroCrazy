using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WorkActionAssetsCheck
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var report = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:RankData", new[] { "Assets" }))
        {
            var rank = AssetDatabase.LoadAssetAtPath<RankData>(AssetDatabase.GUIDToAssetPath(guid));
            if (rank.associatedRole == StaffController.Role.OfficeManager)
                report.Add("Office manager rank " + AssetDatabase.GetAssetPath(rank) + ": " + string.Join(",", rank.unlockedActions.Select(a => a != null ? a.GetType().Name : "NULL")));
        }
        foreach (var path in new[] { "Assets/Prefabs/Clerk.prefab", "Assets/Prefabs/Intern.prefab", "Assets/Prefabs/Guard.prefab" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) { report.Add("Missing prefab: " + path); continue; }
            var references = root.GetComponent<StaffPrefabReferences>();
            var processing = references != null && references.processingIconPrefab != null ? references.processingIconPrefab.GetComponent<ProcessingAnimationUI>() : null;
            report.Add(path + ": processing=" + (processing != null && processing.targetImage != null && processing.animationFrames != null && processing.animationFrames.Count > 0 && processing.animationFrames.All(f => f != null)));
            if (processing != null) report.Add("Processing " + AssetDatabase.GetAssetPath(processing.gameObject) + ": scale=" + processing.transform.localScale + ", offset=" + processing.verticalOffset);
        }
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var hiring = roots.SelectMany(r => r.GetComponentsInChildren<Managers.HiringManager>(true)).Single();
            var shared = hiring.internPrefab.GetComponent<StaffPrefabReferences>();
            var icon = shared != null && shared.processingIconPrefab != null ? shared.processingIconPrefab.GetComponent<ProcessingAnimationUI>() : null;
            report.Add("ACTUAL hired prefab=" + AssetDatabase.GetAssetPath(hiring.internPrefab) + "; references=" + (shared != null) + "; processing=" + (icon != null && icon.targetImage != null && icon.animationFrames.All(f => f != null)));
            foreach (var role in hiring.allRoleData.Where(r => r != null && (r.roleType == StaffController.Role.Janitor || r.roleType == StaffController.Role.ServiceWorker)))
                report.Add("Janitor role " + AssetDatabase.GetAssetPath(role) + ": bag=" + AssetDatabase.GetAssetPath(role.worker_trashBagPrefab));
            var archive = roots.SelectMany(r => r.GetComponentsInChildren<Managers.ArchiveManager>(true)).Single();
            report.Add("Archive: stack=" + (archive.mainDocumentStack != null) + ", paper=" + (archive.mainDocumentStack?.documentPrefab != null) +
                ", root=" + (archive.mainDocumentStack?.stackRoot != null) + ", overflow=" + archive.overflowPoints.Count + ", cabinets=" + archive.cabinets.Count);
            var registry = roots.SelectMany(r => r.GetComponentsInChildren<Managers.ScenePointsRegistry>(true)).Single();
            var desks = roots.SelectMany(r => r.GetComponentsInChildren<ServicePoint>(true));
            foreach (var desk in desks.Where(d => d.documentStack != null))
                report.Add(desk.name + ": pickup=" + (desk.internCollectionPoint != null) + ", paper=" + (desk.documentStack.documentPrefab != null) + ", root=" + (desk.documentStack.stackRoot != null) + ", stack=" + desk.documentStack.transform.position + ", paperPoint=" + (desk.documentPointOnDesk != null ? desk.documentPointOnDesk.position.ToString() : "none"));
            report.Add("Guard report desk=" + (registry.guardReportDesk?.documentStack != null) + "; dumpster=" + (registry.dumpsterPoint != null) + "; toilet=" + (registry.staffToiletPoint != null));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        return string.Join("\n", report);
    }
}
