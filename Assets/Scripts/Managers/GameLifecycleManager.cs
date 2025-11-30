using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Managers
{
    /// <summary>
    /// Отвечает за глобальный цикл: Запуск игры, загрузка сцены, инициализация данных из SaveLoadManager.
    /// </summary>
    public class GameLifecycleManager : MonoBehaviour
    {
        public static GameLifecycleManager Instance { get; private set; }

        [Header("Настройки сцен")]
        [SerializeField] private string gameSceneName = "GameScene";
        [SerializeField] private string mainMenuSceneName = "MainMenuScene";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void StartNewGame(int slotIndex)
        {
            if (SaveLoadManager.Instance == null) return;

            Debug.Log($"[GameLifecycle] Начало НОВОЙ игры в слоте {slotIndex}");
            SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
            SaveLoadManager.Instance.isNewGame = true;
            
            // Создаем базовый сейв
            SaveData initialData = new SaveData { day = 1, money = 1000 };
            SaveLoadManager.Instance.SaveNewGame(slotIndex, initialData);

            LoadGameScene();
        }

        public void LoadSavedGame(int slotIndex)
        {
            if (SaveLoadManager.Instance == null) return;

            Debug.Log($"[GameLifecycle] Загрузка игры из слота {slotIndex}");
            SaveLoadManager.Instance.SetCurrentSlot(slotIndex);
            SaveLoadManager.Instance.isNewGame = false;

            LoadGameScene();
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f; // Снимаем паузу
            
            // Автосохранение перед выходом (если мы в игре)
            if (SceneManager.GetActiveScene().name == gameSceneName && !SaveLoadManager.Instance.isNewGame)
            {
                SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.GetCurrentSlot());
            }

            if (TransitionManager.Instance != null)
                TransitionManager.Instance.TransitionToScene(mainMenuSceneName);
            else
                SceneManager.LoadScene(mainMenuSceneName);
        }

        private void LoadGameScene()
        {
            if (TransitionManager.Instance != null)
                TransitionManager.Instance.TransitionToScene(gameSceneName);
            else
                SceneManager.LoadScene(gameSceneName);
        }
    }
}