// Файл: Assets/Scripts/Managers/AchievementManager.cs

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Managers
{
    public class AchievementManager : MonoBehaviour
    {
        public static AchievementManager Instance { get; private set; }

        [Header("База Данных")]
        [Tooltip("Перетащи сюда ВСЕ ассеты AchievementData, которые есть в игре")]
        public List<AchievementData> allAchievementsDatabase;

        // --- Поле toastUIPrefab УДАЛЕНО ---

        // --- Внутреннее состояние ---
        private HashSet<string> unlockedAchievementIDs = new HashSet<string>();
        private string saveFilePath;

        // Событие, на которое подпишется наш "тост"
        public event System.Action<AchievementData> OnAchievementUnlocked;
        // --- <<< НОВОЕ СОБЫТИЕ ДЛЯ СБРОСА >>> ---
        public event System.Action OnAchievementsReset;
        // --- <<< КОНЕЦ НОВОГО СОБЫТИЯ >>> ---

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // Мы НЕ вызываем DontDestroyOnLoad.
                // Мы полагаемся, что HiringManager сделает нашего родителя [SYSTEMS] бессмертным.

                saveFilePath = Path.Combine(Application.persistentDataPath, "achievements.dat");

                // Запоминаем, был ли уже файл сохранения — это нужно, чтобы применить
                // дефолт-разблокировки (isUnlockedByDefault) ТОЛЬКО при первом запуске.
                bool saveFileExisted = File.Exists(saveFilePath);

                LoadAchievements();

                // Авто-подгрузка ассетов AchievementData из проекта, если в инспекторе
                // ничего не назначено. В редакторе ищем в Assets/Thoughts/Achivments,
                // в билде — в Resources/Thoughts/Achivments.
                EnsureDatabasePopulated();

                // Применяем дефолт-разблокировки только при самом первом запуске
                // (когда файла сохранения ещё не было). Это позволяет дизайнеру
                // пометить ачивки (например, «Мануал» или «Титры») как
                // изначально доступные в архиве.
                if (!saveFileExisted)
                {
                    ApplyDefaultUnlocks();
                    SaveAchievements();
                }
            }
            else if (Instance != this)
            {
                // Мы - дубликат из новой сцены, самоуничтожаемся.
                Destroy(gameObject);
            }
        }

        private void EnsureDatabasePopulated()
        {
            if (allAchievementsDatabase == null) allAchievementsDatabase = new List<AchievementData>();

            // Удаляем null-элементы, если какие-то ссылки потерялись
            for (int i = allAchievementsDatabase.Count - 1; i >= 0; i--)
            {
                if (allAchievementsDatabase[i] == null) allAchievementsDatabase.RemoveAt(i);
            }

            if (allAchievementsDatabase.Count > 0) return; // Уже наполнено вручную — не трогаем

#if UNITY_EDITOR
            // В редакторе можно дотянуться до AssetDatabase
            var guids = UnityEditor.AssetDatabase.FindAssets("t:AchievementData");
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<AchievementData>(path);
                if (asset != null && !allAchievementsDatabase.Contains(asset))
                    allAchievementsDatabase.Add(asset);
            }
#else
            // В билде подтягиваем из Resources
            var resAssets = Resources.LoadAll<AchievementData>("Thoughts/Achivments");
            foreach (var asset in resAssets)
            {
                if (asset != null && !allAchievementsDatabase.Contains(asset))
                    allAchievementsDatabase.Add(asset);
            }
#endif
        }

        /// <summary>
        /// Главный метод для разблокировки достижения.
        /// </summary>
        /// <param name="id">Уникальный ID ачивки (например, "OPEN_FIRST_DOOR")</param>
        public void UnlockAchievement(string id)
        {
            // 1. Проверяем, существует ли такая ачивка в базе
            AchievementData data = allAchievementsDatabase.FirstOrDefault(a => a.achievementID == id);
            if (data == null)
            {
                Debug.LogWarning($"[AchievementManager] Попытка разблокировать несуществующую ачивку: {id}");
                return;
            }

            // 2. Проверяем, не была ли она уже разблокирована
            if (unlockedAchievementIDs.Contains(id))
            {
                // Уже открыта, ничего не делаем
                return; 
            }

            // 3. Разблокируем!
            Debug.Log($"<color=yellow>ДОСТИЖЕНИЕ ПОЛУЧЕНО:</color> {data.displayName}");
            unlockedAchievementIDs.Add(id);
        
            // 4. Сохраняем прогресс на диск
            SaveAchievements();

            // 5. Вызываем событие, чтобы UI-тост мог себя показать
            OnAchievementUnlocked?.Invoke(data);
        }

        /// <summary>
        /// Проверяет, открыта ли ачивка (нужно для UI списка)
        /// </summary>
        public bool IsAchievementUnlocked(string id)
        {
            return unlockedAchievementIDs.Contains(id);
        }

        // --- <<< НОВЫЙ ПУБЛИЧНЫЙ МЕТОД ДЛЯ КНОПКИ >>> ---
        /// <summary>
        /// Сбрасывает все ачивки до нуля.
        /// </summary>
        public void ResetAllAchievements()
        {
            unlockedAchievementIDs.Clear();
            if (File.Exists(saveFilePath))
            {
                File.Delete(saveFilePath);
            }

            Debug.LogWarning("[AchievementManager] ВСЕ АЧИВКИ СБРОШЕНЫ!");

            // Оповещаем UI, чтобы он обновился
            OnAchievementsReset?.Invoke();
        }
        // --- <<< КОНЕЦ НОВОГО МЕТОДА >>> ---

        /// <summary>
        /// Применяет isUnlockedByDefault ко всем ассетам, у которых он выставлен.
        /// Вызывается однократно при первом запуске (когда файла сохранения ещё не было).
        /// </summary>
        private void ApplyDefaultUnlocks()
        {
            if (allAchievementsDatabase == null) return;

            int applied = 0;
            foreach (var data in allAchievementsDatabase)
            {
                if (data == null) continue;
                if (!data.isUnlockedByDefault) continue;
                if (string.IsNullOrEmpty(data.achievementID)) continue;
                if (unlockedAchievementIDs.Contains(data.achievementID)) continue;

                unlockedAchievementIDs.Add(data.achievementID);
                Debug.Log($"[AchievementManager] Дефолт-разблокировка: {data.displayName} ({data.achievementID})");
                applied++;
            }

            if (applied > 0)
            {
                Debug.Log($"[AchievementManager] Применено {applied} дефолт-разблокировок.");
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Тест: поднять тост разблокировки без изменения HashSet и без сохранения на диск.
        /// Только для Editor (используется F12-тумблером в AchievementListUI).
        /// </summary>
        public void DebugRaiseUnlockToast(AchievementData data)
        {
            if (data == null) return;
            OnAchievementUnlocked?.Invoke(data);
        }
#endif


        // --- СИСТЕМА СОХРАНЕНИЯ/ЗАГРУЗКИ (Глобальная) ---

        [System.Serializable]
        private class AchievementSaveData
        {
            // Сохраняем просто список ID
            public List<string> unlockedIDs = new List<string>();
        }

        private void SaveAchievements()
        {
            try
            {
                AchievementSaveData data = new AchievementSaveData();
                data.unlockedIDs = new List<string>(unlockedAchievementIDs);
            
                string json = JsonUtility.ToJson(data);
                File.WriteAllText(saveFilePath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AchievementManager] Не удалось сохранить ачивки: {e.Message}");
            }
        }

        private void LoadAchievements()
        {
            if (!File.Exists(saveFilePath))
            {
                unlockedAchievementIDs = new HashSet<string>();
                return; // Файла нет, начинаем с нуля
            }

            try
            {
                string json = File.ReadAllText(saveFilePath);
                AchievementSaveData data = JsonUtility.FromJson<AchievementSaveData>(json);
                unlockedAchievementIDs = new HashSet<string>(data.unlockedIDs);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AchievementManager] Не удалось загрузить ачивки: {e.Message}");
                unlockedAchievementIDs = new HashSet<string>(); // Сбрасываем в случае ошибки
            }
        }
    }
}