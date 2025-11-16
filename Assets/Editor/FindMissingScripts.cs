using UnityEngine;
using UnityEditor;
using System.Collections;

public class FindMissingScripts : EditorWindow 
{
    [MenuItem("Window/Find Missing Scripts")]
    public static void ShowWindow()
    {
        GetWindow(typeof(FindMissingScripts));
    }

    public void OnGUI()
    {
        if (GUILayout.Button("Find Missing Scripts in Scene"))
        {
            FindInScene();
        }
        
        if (GUILayout.Button("Find Missing Scripts in Project"))
        {
            FindInProject();
        }
    }

    private static void FindInScene()
    {
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        foreach (GameObject gameObject in allObjects)
        {
            Component[] components = gameObject.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null)
                {
                    Debug.LogError("Missing script found on: " + FullPath(gameObject), gameObject);
                }
            }
        }
    }

    private static void FindInProject()
    {
        string[] allPrefabs = AssetDatabase.GetAllAssetPaths();
        foreach (string prefab in allPrefabs)
        {
            if (prefab.Contains(".prefab"))
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                if (go != null)
                {
                    Component[] components = go.GetComponentsInChildren<Component>(true);
                    foreach (Component component in components)
                    {
                        if (component == null)
                        {
                            Debug.LogError("Missing script found in prefab: " + prefab);
                        }
                    }
                }
            }
        }
    }

    private static string FullPath(GameObject go)
    {
        return go.transform.parent == null 
            ? go.name 
            : FullPath(go.transform.parent.gameObject) + "/" + go.name;
    }
}