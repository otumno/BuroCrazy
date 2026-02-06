// Assets/Editor/QuickGameLauncher.cs
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

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

        if (EditorPrefs.GetBool(PREF_MAXIMIZE, false))
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
            if (EditorPrefs.GetBool(PREF_MAXIMIZE, false))
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

    private static void SetGameViewMaximized(bool maximized)
    {
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var gameView = EditorWindow.GetWindow(gameViewType);
        if (gameView != null) gameView.maximized = maximized;
    }

    [MenuItem("Tools/Quick Launcher/Set Current Scene as Start")]
    public static void SetCurrentAsStart()
    {
        string currentPath = EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(currentPath)) return;
        EditorPrefs.SetString(PREF_START_SCENE, currentPath);
        Debug.Log($"QuickLauncher: Start Scene -> {currentPath}");
    }
}