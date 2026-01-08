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

    // --- F5: ЗАПУСК ---
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

        // 1. Запоминаем текущую сцену
        string currentScene = EditorSceneManager.GetActiveScene().path;
        EditorPrefs.SetString(PREF_PREVIOUS_SCENE, currentScene);

        // 2. Maximize (Разворачиваем)
        if (EditorPrefs.GetBool(PREF_MAXIMIZE, false))
        {
            SetGameViewMaximized(true);
        }

        // 3. Открываем и запускаем
        try 
        {
            EditorSceneManager.OpenScene(startScenePath);
            EditorApplication.isPlaying = true;
        }
        catch
        {
            Debug.LogError($"Не удалось найти сцену: {startScenePath}");
        }
    }

    // --- АВТОВОЗВРАТ И СВОРАЧИВАНИЕ ---
    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        // Срабатывает, когда мы вернулись в режим редактора
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            // 1. Un-Maximize (Сворачиваем обратно)
            if (EditorPrefs.GetBool(PREF_MAXIMIZE, false))
            {
                SetGameViewMaximized(false);
            }

            // 2. Возврат сцены
            string prevScene = EditorPrefs.GetString(PREF_PREVIOUS_SCENE);
            
            if (!string.IsNullOrEmpty(prevScene))
            {
                if (EditorSceneManager.GetActiveScene().path != prevScene)
                {
                    EditorSceneManager.OpenScene(prevScene);
                    Debug.Log($"<color=green>QuickLauncher: Возврат в {prevScene}</color>");
                }
                EditorPrefs.DeleteKey(PREF_PREVIOUS_SCENE);
            }
        }
    }

    // Вспомогательный метод для управления окном GameView
    private static void SetGameViewMaximized(bool maximized)
    {
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var gameView = EditorWindow.GetWindow(gameViewType);
        
        if (gameView != null)
        {
            gameView.maximized = maximized;
        }
    }

    // --- МЕНЮ ---
    [MenuItem("Tools/Quick Launcher/Set Current Scene as Start")]
    public static void SetCurrentAsStart()
    {
        string currentPath = EditorSceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(currentPath))
        {
            Debug.LogError("Сначала сохраните сцену (Ctrl+S)!");
            return;
        }
        EditorPrefs.SetString(PREF_START_SCENE, currentPath);
        Debug.Log($"QuickLauncher: Стартовая сцена -> <b>{currentPath}</b>");
    }

    [MenuItem("Tools/Quick Launcher/Toggle Maximize on Play")]
    public static void ToggleMaximize()
    {
        bool current = EditorPrefs.GetBool(PREF_MAXIMIZE, false);
        EditorPrefs.SetBool(PREF_MAXIMIZE, !current);
        Debug.Log($"QuickLauncher: Maximize on Play = {!current}");
    }
    
    [MenuItem("Tools/Quick Launcher/Toggle Maximize on Play", true)]
    public static bool ToggleMaximizeValidate()
    {
        Menu.SetChecked("Tools/Quick Launcher/Toggle Maximize on Play", EditorPrefs.GetBool(PREF_MAXIMIZE, false));
        return true;
    }
}