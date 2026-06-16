// Assets/Editor/QuickGameLauncher.cs
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Reflection;

[InitializeOnLoad]
public class QuickGameLauncher
{
    private const string PREF_START_SCENE = "QuickLauncher_StartScene";
    private const string PREF_PREVIOUS_SCENE = "QuickLauncher_PreviousScene";
    private const string PREF_MAXIMIZE = "QuickLauncher_Maximize";

    static QuickGameLauncher()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Quick Launcher/Play Start Scene _F5")]
    public static void PlayStartScene()
    {
        string startScenePath = EditorPrefs.GetString(PREF_START_SCENE);
        if (string.IsNullOrEmpty(startScenePath))
        {
            if (EditorUtility.DisplayDialog("Quick Launcher",
                "Стартовая сцена не выбрана!\nTools > Quick Launcher > Set Current Scene as Start",
                "Понял")) { }
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // ЗАЩИТА: Гарантируем, что редактор не стоит на паузе перед запуском
        EditorApplication.isPaused = false;

        string currentScene = EditorSceneManager.GetActiveScene().path;
        EditorPrefs.SetString(PREF_PREVIOUS_SCENE, currentScene);

        // По умолчанию maximize включён, если флаг не задан
        if (!EditorPrefs.HasKey(PREF_MAXIMIZE))
        {
            EditorPrefs.SetBool(PREF_MAXIMIZE, true);
        }

        if (EditorPrefs.GetBool(PREF_MAXIMIZE, true))
        {
            SetGameViewMaximized(true);
        }

        try
        {
            EditorSceneManager.OpenScene(startScenePath);
            EditorApplication.isPlaying = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"QuickLauncher Error: {e.Message}");
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            if (EditorPrefs.GetBool(PREF_MAXIMIZE, true))
            {
                SetGameViewMaximized(false);
            }

            string prevScene = EditorPrefs.GetString(PREF_PREVIOUS_SCENE);
            if (!string.IsNullOrEmpty(prevScene))
            {
                if (EditorSceneManager.GetActiveScene().path != prevScene)
                {
                    EditorSceneManager.OpenScene(prevScene);
                }
                EditorPrefs.DeleteKey(PREF_PREVIOUS_SCENE);
            }
        }

        // ЗАЩИТА: Если при входе в Play Mode редактор сам нажал паузу — отжимаем её
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            EditorApplication.isPaused = false;
        }
    }

    /// <summary>
    /// Maximize GameView через reflection. Универсально работает в Unity 2021+.
    /// </summary>
    private static void SetGameViewMaximized(bool maximized)
    {
        try
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;

            var gameView = EditorWindow.GetWindow(gameViewType);
            if (gameView == null) return;

            // Способ 1: свойство maximized (Unity 2019.3+)
            var maxProp = gameViewType.GetProperty("maximized",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (maxProp != null && maxProp.CanWrite)
            {
                maxProp.SetValue(gameView, maximized);
                gameView.Repaint();
                return;
            }

            // Способ 2: статический метод SetMainGameViewSize (fallback)
            var setSizeMethod = gameViewType.GetMethod("SetMainGameViewSize",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (setSizeMethod != null)
            {
                // Получаем текущее разрешение
                var sizeProp = gameViewType.GetProperty("currentGameViewSize",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (sizeProp != null)
                {
                    var size = sizeProp.GetValue(gameView);
                    setSizeMethod.Invoke(null, new object[] { size, false, maximized });
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[QuickGameLauncher] Failed to set maximized={maximized}: {e.Message}");
        }
    }

    [MenuItem("Tools/Quick Launcher/Set Current Scene as Start")]
    public static void SetCurrentAsStart()
    {
        string currentPath = EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(currentPath)) return;
        EditorPrefs.SetString(PREF_START_SCENE, currentPath);
        Debug.Log($"QuickLauncher: Start Scene -> {currentPath}");
    }

    [MenuItem("Tools/Quick Launcher/Toggle Maximize on Play")]
    public static void ToggleMaximizeOnPlay()
    {
        bool current = EditorPrefs.GetBool(PREF_MAXIMIZE, true);
        EditorPrefs.SetBool(PREF_MAXIMIZE, !current);
        Debug.Log($"QuickLauncher: Maximize on Play = {!current}");
    }

    [MenuItem("Tools/Quick Launcher/Toggle Maximize on Play", validate = true)]
    private static bool ToggleMaximizeOnPlayValidate()
    {
        bool current = EditorPrefs.GetBool(PREF_MAXIMIZE, true);
        Menu.SetChecked("Tools/Quick Launcher/Toggle Maximize on Play", current);
        return true;
    }
}
