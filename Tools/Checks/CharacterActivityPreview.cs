using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CharacterActivityPreview
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null;
        Texture2D image = null;
        var previousTarget = RenderTexture.active;
        try
        {
            var paths = new[] { "Assets/Prefabs/Client.prefab", "Assets/Prefabs/Director.prefab", "Assets/Prefabs/Clerk.prefab" };
            var actors = paths.Select(p => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(p), scene)).ToArray();
            var client = actors[0].GetComponent<ClientPathfinding>();
            client.name = "Клиент №2";
            var sm = actors[0].GetComponent<ClientStateMachine>();
            client.stateMachine = sm;
            Set(sm, "parent", client);
            Set(sm, "currentState", ClientState.AtWaitingArea);
            Set(sm, "myQueueNumber", 2);
            var deskObject = new GameObject("PreviewDesk");
            SceneManager.MoveGameObjectToScene(deskObject, scene);
            var desk = deskObject.AddComponent<ServicePoint>();
            desk.friendlyName = "Регистратура";
            var director = actors[1].GetComponent<DirectorAvatarController>();
            Set(director, "currentWorkstation", desk);
            Set(director, "clientBeingServed", client);
            var worker = actors[2].GetComponent<StaffController>();
            worker.name = "Анна Петрова";
            worker.nameData = new StaffController.StaffNameData { firstName = "Анна", lastName = "Петрова", shortName = "Анна П." };
            worker.currentRole = StaffController.Role.Clerk;
            var mover = actors[2].GetComponent<AgentMover>();
            Set(mover, "isSlipping", true);
            Set(mover, "<FallReason>k__BackingField", "поскользнулся на луже");

            var labels = new TextMeshProUGUI[3];
            for (int i = 0; i < actors.Length; ++i)
            {
                actors[i].transform.position = new Vector3((i - 1) * 3.5f, -1.5f, 0f);
                CharacterActivityOverlay.Ensure(actors[i]);
                var overlay = actors[i].GetComponent<CharacterActivityOverlay>();
                if (Get(overlay, "root") == null) Invoke(overlay, "Awake");
                Invoke(overlay, "LateUpdate");
                labels[i] = actors[i].transform.Find("ActivityStatus/StatusText").GetComponent<TextMeshProUGUI>();
                labels[i].ForceMeshUpdate(true, true);
                if (labels[i].raycastTarget || labels[i].isTextOverflowing || string.IsNullOrWhiteSpace(labels[i].text))
                    throw new InvalidOperationException("Status label is empty, overflowing or blocks clicks.");
                foreach (char c in labels[i].text.Where(c => c >= 'А' && c <= 'я' || c == 'ё' || c == 'Ё'))
                    if (!labels[i].font.HasCharacter(c, true, false)) throw new InvalidOperationException("Missing Cyrillic glyph: " + c);
            }
            if (!labels[0].text.Contains("Ждёт вызова") || !labels[1].text.Contains("Обслуживает") || !labels[2].text.Contains("поскользнулся на луже"))
                throw new InvalidOperationException("Activity does not match the character's real state.");

            var cameraObject = new GameObject("StatusPreviewCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.cameraType = CameraType.Game;
            camera.scene = scene;
            camera.transform.position = new Vector3(0f, 1f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 3.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.18f, .19f, .22f);
            target = new RenderTexture(1600, 900, 24);
            camera.targetTexture = target;
            foreach (var label in labels) label.canvas.worldCamera = camera;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            var output = Path.GetFullPath("Temp/CharacterActivityPreview.png");
            File.WriteAllBytes(output, image.EncodeToPNG());
            return "PASS: actual client/director/worker prefabs, Cyrillic, state text, no overflow or click interception. Preview: " + output + "\n" +
                string.Join("\n", labels.Select(l => l.text + " position=" + l.transform.position + " scale=" + l.transform.lossyScale + " active=" + l.gameObject.activeInHierarchy + " vertices=" + l.textInfo.meshInfo[0].vertexCount));
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
