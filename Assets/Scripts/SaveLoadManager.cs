using UnityEngine;
using System.IO;

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }

    private int currentSlotIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }

    public void SaveGame(int slotIndex)
    {
        currentSlotIndex = slotIndex;
        SaveData data = new SaveData();
        
        if (ClientSpawner.Instance != null)
            data.day = ClientSpawner.Instance.GetCurrentDay();

        if (PlayerWallet.Instance != null)
            data.money = PlayerWallet.Instance.GetCurrentMoney();
        
        if (ArchiveManager.Instance != null)
            data.archiveDocumentCount = ArchiveManager.Instance.GetCurrentDocumentCount();

        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
        File.WriteAllText(path, json);
        
        PlayerPrefs.SetInt("LastUsedSlot", slotIndex);
        Debug.Log($"Игра сохранена в слот {slotIndex}");
    }

    public bool LoadGame(int slotIndex)
    {
        string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");

        if (File.Exists(path))
        {
            currentSlotIndex = slotIndex;
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            if (ClientSpawner.Instance != null)
                ClientSpawner.Instance.SetDay(data.day);

            if (PlayerWallet.Instance != null)
                PlayerWallet.Instance.SetMoney(data.money);
            
            if (ArchiveManager.Instance != null)
                ArchiveManager.Instance.SetDocumentCount(data.archiveDocumentCount);

            PlayerPrefs.SetInt("LastUsedSlot", slotIndex);
            Debug.Log($"Игра загружена из слота {slotIndex}");
            return true;
        }
        else
        {
            Debug.LogWarning($"Файл сохранения для слота {slotIndex} не найден!");
            return false;
        }
    }
    
    public bool GetLastSavedSlot(out int lastSlotIndex)
    {
        if (PlayerPrefs.HasKey("LastUsedSlot"))
        {
            lastSlotIndex = PlayerPrefs.GetInt("LastUsedSlot");
            return DoesSaveExist(lastSlotIndex);
        }
        lastSlotIndex = -1;
        return false;
    }

    public SaveData GetDataForSlot(int slotIndex)
    {
        string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<SaveData>(json);
        }
        return null;
    }

    public void DeleteSave(int slotIndex)
    {
        string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"Сохранение в слоте {slotIndex} удалено.");
        }
    }

    public bool DoesSaveExist(int slotIndex)
    {
        string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
        return File.Exists(path);
    }
}