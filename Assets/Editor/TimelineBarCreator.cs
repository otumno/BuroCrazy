#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class TimelineBarCreator
{
    [MenuItem("Tools/Create Timeline Bar Prefab")]
    static void CreatePrefab()
    {
        // Создаём корневой объект
        GameObject root = new GameObject("[UI] TimelineBar");
        root.AddComponent<RectTransform>();

        // Маркер текущего времени
        GameObject marker = new GameObject("CurrentTimeMarker", typeof(Image));
        marker.transform.SetParent(root.transform);
        var markerRt = marker.GetComponent<RectTransform>();
        markerRt.anchorMin = new Vector2(0, 0.5f);
        markerRt.anchorMax = new Vector2(0, 0.5f);
        markerRt.sizeDelta = new Vector2(2, 40);
        markerRt.anchoredPosition = Vector2.zero;

        // Контейнер для фонов периодов
        GameObject periodsCon = new GameObject("PeriodsContainer", typeof(RectTransform));
        periodsCon.transform.SetParent(root.transform);

        // Контейнер для иконок клиентов
        GameObject iconsCon = new GameObject("IconsContainer", typeof(RectTransform));
        iconsCon.transform.SetParent(root.transform);

        // Скрипт контроллера (периоды будут загружены из TimeManager в рантайме)
        var controller = root.AddComponent<TimelineController>();
        controller.currentTimeMarker = markerRt;
        controller.periodsContainer = periodsCon.transform;
        controller.iconsContainer = iconsCon.transform;

        // Сохраняем как префаб
        string path = "Assets/Prefabs/[UI] TimelineBar.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        Debug.Log("TimelineBar prefab created at " + path);
    }
}
#endif