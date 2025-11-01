// Файл: Assets/Scripts/Managers/AchievementManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            LoadAchievements();
        }
        else if (Instance != this)
        {
            // Мы - дубликат из новой сцены, самоуничтожаемся.
            Destroy(gameObject);
        }
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