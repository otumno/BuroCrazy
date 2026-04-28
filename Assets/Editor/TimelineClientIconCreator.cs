#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class TimelineClientIconCreator
{
    [MenuItem("Tools/Create Timeline Client Icon Prefab")]
    static void CreatePrefab()
    {
        GameObject go = new GameObject("TimelineClientIcon", typeof(Image));
        var img = go.GetComponent<Image>();
        img.sprite = null; // назначишь позже
        img.color = Color.white;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(12, 18); // тонкая высокая фигурка
        string path = "Assets/Prefabs/[UI] TimelineClientIcon.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("TimelineClientIcon prefab created at " + path);
    }
}
#endif