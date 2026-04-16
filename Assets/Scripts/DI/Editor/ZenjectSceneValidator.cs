using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;

namespace DI.Editor
{
    /// <summary>
    /// Editor-скрипт для проверки наличия SceneContext на сценах.
    /// Помогает убедиться, что все сцены правильно настроены для Zenject.
    /// </summary>
    public class ZenjectSceneValidator : EditorWindow
    {
        [MenuItem("BuroCrazy/DI/Validate Scene Contexts")]
        public static void ShowWindow()
        {
            GetWindow<ZenjectSceneValidator>("Zenject Validator");
        }

        [MenuItem("BuroCrazy/DI/Add SceneContext to Current Scene")]
        public static void AddSceneContextToCurrentScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            bool hasSceneContext = rootObjects.Any(go =>
                go.GetComponent<Zenject.SceneContext>() != null);

            if (hasSceneContext)
            {
                Debug.Log("[ZenjectSceneValidator] SceneContext уже существует на сцене.");
                return;
            }

            // Создаём новый GameObject с SceneContext
            GameObject sceneContextObj = new GameObject("SceneContext");
            sceneContextObj.AddComponent<Zenject.SceneContext>();

            // Также добавляем DIBindingValidator
            sceneContextObj.AddComponent<DIBindingValidator>();

            Debug.Log($"[ZenjectSceneValidator] Создан SceneContext на сцене {scene.name}");
        }

        [MenuItem("BuroCrazy/DI/Create ProjectContext Prefab")]
        public static void CreateProjectContextPrefab()
        {
            // Проверяем существование Resources папки
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
                Debug.Log("[ZenjectSceneValidator] Создана папка Assets/Resources");
            }

            // Проверяем существование префаба
            string prefabPath = "Assets/Resources/ProjectContext.prefab";
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (existingPrefab != null)
            {
                Debug.LogWarning("[ZenjectSceneValidator] Префаб ProjectContext.prefab уже существует!");
                Selection.activeObject = existingPrefab;
                return;
            }

            // Создаём новый GameObject для префаба
            GameObject projectContextObj = new GameObject("ProjectContext");
            projectContextObj.AddComponent<Zenject.ProjectContext>();
            projectContextObj.AddComponent<ProjectContextInstaller>();

            // Создаём префаб
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(projectContextObj, prefabPath);

            if (prefab != null)
            {
                Debug.Log($"[ZenjectSceneValidator] Создан префаб ProjectContext по пути: {prefabPath}");
                Selection.activeObject = prefab;
            }
            else
            {
                Debug.LogError("[ZenjectSceneValidator] Не удалось создать префаб!");
            }

            // Удаляем временный объект
            DestroyImmediate(projectContextObj);
        }

        [MenuItem("BuroCrazy/DI/Create Installer Asset")]
        public static void CreateInstallerAsset()
        {
            string assetPath = "Assets/Scripts/DI/ProjectContextInstallerAsset.asset";
            var existingAsset = AssetDatabase.LoadAssetAtPath<ProjectContextInstallerAsset>(assetPath);

            if (existingAsset != null)
            {
                Debug.LogWarning("[ZenjectSceneValidator] Asset уже существует!");
                Selection.activeObject = existingAsset;
                return;
            }

            var installerAsset = ScriptableObject.CreateInstance<ProjectContextInstallerAsset>();
            AssetDatabase.CreateAsset(installerAsset, assetPath);

            Debug.Log($"[ZenjectSceneValidator] Создан installer asset: {assetPath}");
            Selection.activeObject = installerAsset;
        }

        public void OnGUI()
        {
            GUILayout.Label("Zenject DI Integration Checker", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (GUILayout.Button("Validate All Open Scenes", GUILayout.Height(30)))
            {
                ValidateAllScenes();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Add SceneContext to Current Scene", GUILayout.Height(25)))
            {
                AddSceneContextToCurrentScene();
            }

            if (GUILayout.Button("Create ProjectContext Prefab", GUILayout.Height(25)))
            {
                CreateProjectContextPrefab();
            }

            if (GUILayout.Button("Create Installer Asset", GUILayout.Height(25)))
            {
                CreateInstallerAsset();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Порядок действий:\n" +
                "1. Нажми 'Create Installer Asset'\n" +
                "2. Перетащи asset в ProjectContext > Installers\n" +
                "3. Добавь SceneContext на каждую сцену\n" +
                "4. Запусти игру и проверь консоль",
                MessageType.Info);
        }

        private static void ValidateAllScenes()
        {
            var sceneCount = EditorSceneManager.sceneCountInBuildSettings;
            Debug.Log($"[ZenjectSceneValidator] Проверяю {sceneCount} сцен...");

            for (int i = 0; i < sceneCount; i++)
            {
                var scenePath = EditorSceneManager.GetSceneByBuildIndex(i).path;
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var rootObjects = scene.GetRootGameObjects();

                bool hasSceneContext = rootObjects.Any(go =>
                    go.GetComponent<Zenject.SceneContext>() != null);

                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                if (hasSceneContext)
                {
                    Debug.Log($"<color=green>[OK]</color> {sceneName}");
                }
                else
                {
                    Debug.LogWarning($"<color=red>[MISSING SCENE CONTEXT]</color> {sceneName}");
                }
            }

            Debug.Log("[ZenjectSceneValidator] Валидация завершена!");
        }
    }
}
