using System.Linq;
using TMPro;
using UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StaffShiftHUDBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/StaffShiftHUD.prefab";
    private static readonly Color Ink = new(.95f, .91f, .82f);

    [MenuItem("Bureau/Create Staff Shift HUD")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Use Edit Mode");
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Additive);
        try
        {
            var canvas = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true))
                .Single(c => c.name == "Canvas Game");
            var font = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TMP_Text>(true))
                .Where(t => t.font != null).Select(t => t.font).FirstOrDefault() ?? TMP_Settings.defaultFontAsset;
            var root = Rect("StaffShiftHUD", null);
            try
            {
                root.anchorMin = new Vector2(1, .18f);
                root.anchorMax = new Vector2(1, .82f);
                root.pivot = new Vector2(1, 1);
                root.sizeDelta = new Vector2(264, 0);
                root.anchoredPosition = new Vector2(-16, 0);
                Background(root, new Color(.16f, .14f, .12f, .94f));
                var title = Text("Title", root, font, 18);
                Place(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(10, -34), new Vector2(-10, -4));
                var viewport = Rect("Viewport", root);
                Place(viewport, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -38));
                viewport.gameObject.AddComponent<RectMask2D>();
                Background(viewport, Color.clear).raycastTarget = true;
                var content = Rect("Content", viewport);
                content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
                content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 5; layout.childControlWidth = true; layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
                var empty = Text("Empty", content, font, 14); empty.text = "Сотрудники ещё не вышли";
                empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 32;
                var row = MakeRow(root, font);
                row.gameObject.SetActive(false);
                var hud = root.gameObject.AddComponent<StaffShiftHUD>();
                Set(hud, "rowTemplate", row); Set(hud, "content", content); Set(hud, "title", title);
                Set(hud, "emptyLabel", empty.gameObject);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
                var existing = canvas.GetComponentInChildren<StaffShiftHUD>(true);
                // Keep scene overrides on repeated builds; updating the prefab refreshes its instance.
                if (existing == null) PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new System.Exception("HUD scene save failed");
                Debug.Log("[StaffHUDBuilder] Prefab and GameScene instance saved.");
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    private static StaffShiftRowUI MakeRow(Transform parent, TMP_FontAsset font)
    {
        var root = Rect("RowTemplate", parent);
        root.gameObject.AddComponent<LayoutElement>().preferredHeight = 108;
        Background(root, new Color(.24f, .20f, .16f, 1));
        var role = Rect("RoleColor", root);
        Place(role, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(4, 0));
        var roleImage = Background(role, Color.gray);
        var portraitRect = Rect("Portrait", root);
        Place(portraitRect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -62), new Vector2(52, -6));
        portraitRect.gameObject.AddComponent<RectMask2D>();
        var frameImage = Background(portraitRect, Color.gray);
        var portraitContent = Rect("PortraitContent", portraitRect);
        Place(portraitContent, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
        Background(portraitContent, new Color(.16f, .14f, .12f));
        var portrait = portraitContent.gameObject.AddComponent<StaffPortraitUI>();
        foreach (var layer in new[]{"body", "outfit", "hair", "face", "accessory"})
        {
            var rect = Rect(layer, portraitContent); Place(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = Background(rect, Color.white); img.preserveAspect = true;
            Set(portrait, layer, img);
        }
        var name = Text("Name", root, font, 15);
        Place(name.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(58, -25), new Vector2(-6, -4));
        name.fontStyle = FontStyles.Bold;
        var roleText = Text("Role", root, font, 12);
        Place(roleText.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(58, -43), new Vector2(-6, -25));
        var remaining = Text("Remaining", root, font, 12);
        Place(remaining.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(58, -61), new Vector2(-6, -43));
        var bladder = Bar("Bladder", root, font, 66, new Color(.35f, .56f, .61f), out var bladderText);
        var stress = Bar("Stress", root, font, 87, new Color(.69f, .38f, .29f), out var stressText);
        var row = root.gameObject.AddComponent<StaffShiftRowUI>();
        var colors = AssetDatabase.LoadAssetAtPath<RoleColorDatabase>("Assets/Thoughts/Roledata/RoleColorDatabase.asset");
        if (colors == null) throw new System.Exception("RoleColorDatabase missing");
        Set(row, "portraitFrame", frameImage); Set(row, "roleColors", colors);
        Set(row, "portrait", portrait); Set(row, "roleColor", roleImage); Set(row, "nameText", name);
        Set(row, "roleText", roleText); Set(row, "remainingText", remaining);
        Set(row, "bladderFill", bladder); Set(row, "stressFill", stress);
        Set(row, "bladderText", bladderText); Set(row, "stressText", stressText);
        return row;
    }

    private static RectTransform Bar(string name, Transform parent, TMP_FontAsset font, float top, Color color, out TMP_Text label)
    {
        var track = Rect(name, parent);
        Place(track, new Vector2(0, 1), Vector2.one, new Vector2(9, -top - 16), new Vector2(-8, -top));
        Background(track, new Color(.11f, .10f, .09f));
        var fill = Rect("Fill", track); Place(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Background(fill, color);
        var text = Text("Label", track, font, 11); text.alignment = TextAlignmentOptions.Center;
        Place(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        label = text;
        return fill;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    private static Image Background(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.color = Ink; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }
    private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
    { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = low; rect.offsetMax = high; }
    private static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
