using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BookkeepingNextPreview
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        var previousTarget = RenderTexture.active;
        RenderTexture target = null;
        Texture2D image = null;
        try
        {
            var host = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(host, scene);
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            // DirectorsOffice_UI uses a 3840x2160 reference resolution in GameScene.
            ((RectTransform)host.transform).sizeDelta = new Vector2(3840, 2160);
            host.transform.localScale = Vector3.one * .005f;
            var root = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DirectorUi/BookkeepingPanel.prefab"), scene);
            root.transform.SetParent(host.transform, false);
            root.SetActive(true);
            var group = root.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1;
            var panel = root.GetComponent<UI.Bookkeeping.BookkeepingPanelUI>();
            typeof(UI.Bookkeeping.BookkeepingPanelUI).GetMethod("SetDayResultsMode", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, new object[] { true });
            var data = new SerializedObject(panel);
            var button = (UnityEngine.UI.Button)data.FindProperty("nextButton").objectReferenceValue;
            var label = button.GetComponentInChildren<TMPro.TMP_Text>(true);
            Canvas.ForceUpdateCanvases();
            label.ForceMeshUpdate(true, true);
            if (!button.gameObject.activeSelf || label.text != "Далее" || label.isTextOverflowing)
                throw new InvalidOperationException($"Next button active={button.gameObject.activeSelf}, text={label.text}, overflow={label.isTextOverflowing}, rect={label.rectTransform.rect}, font={label.fontSize}.");
            var cameraObject = new GameObject("Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.cameraType = CameraType.Game;
            camera.scene = scene;
            camera.transform.position = new Vector3(0, 0, -20);
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.1f, .1f, .1f);
            canvas.worldCamera = camera;
            target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            var path = Path.GetFullPath("Temp/BookkeepingNextPreview.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            return "PASS: Next button visible without text overflow. " + path;
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
