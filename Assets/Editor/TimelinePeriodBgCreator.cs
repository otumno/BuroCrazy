#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class TimelinePeriodBgCreator
{
    [MenuItem("Tools/Create Timeline Period Bg Prefab")]
    static void CreatePrefab()
    {
        GameObject go = new GameObject("TimelinePeriodBg", typeof(Image));
        var img = go.GetComponent<Image>();
        img.sprite = null; // будет закрашиваться цветом
        img.type = Image.Type.Sliced; // чтобы растягивался без искажений
        img.color = Color.gray;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 20); // временная ширина, высота полоски
        string path = "Assets/Prefabs/[UI] TimelinePeriodBg.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("TimelinePeriodBg prefab created at " + path);
    }
}
#endif