using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SceneAutoLoader
{
    // Статический конструктор, который вызывается один раз при запуске редактора
    static SceneAutoLoader()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // Если мы нажимаем "Play" в редакторе
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // Сохраняем текущую сцену, чтобы не потерять изменения
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            
            // Запоминаем, из какой сцены мы стартовали, чтобы вернуться в нее
            EditorPrefs.SetString("SceneAutoLoader.PreviousScene", SceneManager.GetActiveScene().path);

            // Ищем нашу главную сцену в настройках билда
            Scene sceneToLoad = default;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path.Contains("MainMenuScene"))
                {
                    sceneToLoad = SceneManager.GetSceneByPath(scene.path);
                    break;
                }
            }

            // Если нашли - загружаем ее
            if (sceneToLoad != default)
            {
                EditorSceneManager.OpenScene(sceneToLoad.path);
            }
        }
        // Если мы нажимаем "Stop"
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            // Возвращаемся в ту сцену, из которой запускали игру
            string previousScenePath = EditorPrefs.GetString("SceneAutoLoader.PreviousScene");
            if (!string.IsNullOrEmpty(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath);
            }
        }
    }
}