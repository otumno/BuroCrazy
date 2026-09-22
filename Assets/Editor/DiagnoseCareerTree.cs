// Assets/Editor/DiagnoseCareerTree.cs
// Утилита для диагностики состояния дерева карьеры.
// Показывает: наличие JobTreeConnectionVisualizer, его активность,
// количество JobNodeUI, состояние Play Mode и Gizmos.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UI.Map;
using UnityEditor;
using UnityEngine;

public static class DiagnoseCareerTree
{
    [MenuItem("Tools/Bureau/Diagnose Career Tree")]
    public static void Diagnose()
    {
        Debug.Log("========== CAREER TREE DIAGNOSTICS ==========");

        // 1. Поиск TreeZone
        GameObject treeZone = null;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            treeZone = FindRecursive(root.transform, "TreeZone");
            if (treeZone != null) break;
        }

        if (treeZone == null)
        {
            Debug.LogError("[Diag] TreeZone НЕ НАЙДЕН в сцене!");
            return;
        }
        Debug.Log($"[Diag] TreeZone найден: {GetPath(treeZone.transform)}, activeInHierarchy={treeZone.activeInHierarchy}");

        // 2. Проверка активности родителей
        Transform t = treeZone.transform;
        while (t != null)
        {
            Debug.Log($"[Diag] Parent '{t.name}' activeSelf={t.gameObject.activeSelf}");
            t = t.parent;
        }

        // 3. JobTreeConnectionVisualizer
        var visualizers = treeZone.GetComponents<JobTreeConnectionVisualizer>();
        Debug.Log($"[Diag] JobTreeConnectionVisualizer count: {visualizers.Length}");
        for (int i = 0; i < visualizers.Length; i++)
        {
            var v = visualizers[i];
            Debug.Log($"[Diag]   Visualizer[{i}]: enabled={v.enabled}, activeSelf={v.gameObject.activeSelf}, " +
                      $"gameObject={v.gameObject.name}, instanceID={v.GetInstanceID()}");
            // Получить private field drawConnections через reflection
            var field = typeof(JobTreeConnectionVisualizer).GetField("drawConnections",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                bool drawConnections = (bool)field.GetValue(v);
                Debug.Log($"[Diag]     drawConnections={drawConnections}");
            }
        }

        // 4. JobNodeUI
        var nodes = treeZone.GetComponentsInChildren<JobNodeUI>(includeInactive: true);
        Debug.Log($"[Diag] JobNodeUI count: {nodes.Length}");
        foreach (var n in nodes.Take(3))
        {
            if (n == null || n.jobData == null) continue;
            Debug.Log($"[Diag]   Node: '{n.gameObject.name}' jobID='{n.jobData.jobID}' " +
                      $"position={n.transform.position} parent={n.transform.parent?.name}");
        }

        // 5. Application.isPlaying
        Debug.Log($"[Diag] Application.isPlaying={Application.isPlaying}");

        // 6. Scene View Gizmos
        bool gizmosEnabled = UnityEditor.SceneView.lastActiveSceneView != null
            && UnityEditor.SceneView.lastActiveSceneView.drawGizmos;
        Debug.Log($"[Diag] SceneView.drawGizmos={gizmosEnabled}");

        Debug.Log("============================================");
    }

    private static GameObject FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent.gameObject;
        for (int i = 0; i < parent.childCount; i++)
        {
            var f = FindRecursive(parent.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }

    private static string GetPath(Transform t)
    {
        if (t == null) return "<null>";
        if (t.parent == null) return t.name;
        return GetPath(t.parent) + "/" + t.name;
    }
}
#endif
