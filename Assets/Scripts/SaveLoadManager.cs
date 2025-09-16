using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; set; }

    [Tooltip("Сколько слотов сохранения будет в игре")]
    public int numberOfSlots = 3; // <-- Вот переменная, которой нам не хватало
    private int currentSlotIndex = 0;
    public bool isNewGame = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    // ... ВСЕ ВАШИ МЕТОДЫ (SaveGame, LoadGame, DeleteSave и т.д.) ОСТАЮТСЯ ЗДЕСЬ ...
    // Я скопировал их из вашего файла без изменений.
    
    public void SaveGame(int slotIndex)
    {
		isNewGame = false;
        currentSlotIndex = slotIndex;
        SaveData data = new SaveData();
        
        data.day = ClientSpawner.Instance.GetCurrentDay();
        data.money = PlayerWallet.Instance.GetCurrentMoney();
        data.archiveDocumentCount = ArchiveManager.Instance.GetCurrentDocumentCount();

        if (DirectorManager.Instance != null)
        {
            data.activePermanentOrderNames = DirectorManager.Instance.activePermanentOrders.Select(order => order.name).ToList();
            data.completedOneTimeOrderNames = DirectorManager.Instance.completedOneTimeOrders.Select(order => order.name).ToList();
        }

        data.allStaffData = new List<StaffSaveData>();
        StaffController[] allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
        foreach (var staffMember in allStaff)
        {
            StaffSaveData staffData = new StaffSaveData();
            staffData.characterName = staffMember.gameObject.name;
            staffData.position = staffMember.transform.position;
            staffData.stressLevel = staffMember.GetStressValue();
            data.allStaffData.Add(staffData);
        }

        data.allDocumentStackData = new List<DocumentStackSaveData>();
        DocumentStack[] allStacks = FindObjectsByType<DocumentStack>(FindObjectsSortMode.None);
        foreach (var stack in allStacks)
        {
            if (ArchiveManager.Instance != null && stack == ArchiveManager.Instance.mainDocumentStack) continue;
            DocumentStackSaveData stackData = new DocumentStackSaveData();
            stackData.stackOwnerName = stack.gameObject.name;
            stackData.documentCount = stack.CurrentSize;
            data.allDocumentStackData.Add(stackData);
        }
        
        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
        File.WriteAllText(path, json);
        
        PlayerPrefs.SetInt("LastUsedSlot", slotIndex);
        Debug.Log($"Игра сохранена в слот {slotIndex}");
    }

    public void SetCurrentSlot(int slotIndex)
    {
        currentSlotIndex = slotIndex;
        Debug.Log($"[SaveLoadManager] Текущий слот изменен на {slotIndex}");
    }

    public bool LoadGame(int slotIndex)
    {
		isNewGame = false;
        string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
        if (File.Exists(path))
        {
            currentSlotIndex = slotIndex;
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            ClientSpawner.Instance.SetDay(data.day);
            PlayerWallet.Instance.SetMoney(data.money);
            ArchiveManager.Instance.SetDocumentCount(data.archiveDocumentCount);

            if (DirectorManager.Instance != null)
            {
                var allOrders = DirectorManager.Instance.allOrders;
                DirectorManager.Instance.activePermanentOrders.Clear();
                DirectorManager.Instance.completedOneTimeOrders.Clear();

                if (data.activePermanentOrderNames != null)
                {
                    foreach (string orderName in data.activePermanentOrderNames)
                    {
                        DirectorOrder orderAsset = allOrders.FirstOrDefault(o => o.name == orderName);
                        if (orderAsset != null)
                        {
                            DirectorManager.Instance.activePermanentOrders.Add(orderAsset);
                        }
                    }
                }
                
                if (data.completedOneTimeOrderNames != null)
                {
                    foreach (string orderName in data.completedOneTimeOrderNames)
                    {
                        DirectorOrder orderAsset = allOrders.FirstOrDefault(o => o.name == orderName);
                        if (orderAsset != null)
                        {
                            DirectorManager.Instance.completedOneTimeOrders.Add(orderAsset);
                        }
                    }
                }
            }

            StaffController[] allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
            foreach (var staffData in data.allStaffData)
            {
                StaffController staffMember = allStaff.FirstOrDefault(s => s.gameObject.name == staffData.characterName);
                if (staffMember != null)
                {
                    staffMember.transform.position = staffData.position;
                    staffMember.SetStressValue(staffData.stressLevel);
                }
            }

            DocumentStack[] allStacks = FindObjectsByType<DocumentStack>(FindObjectsSortMode.None);
            foreach (var stackData in data.allDocumentStackData)
            {
                DocumentStack stack = allStacks.FirstOrDefault(s => s.gameObject.name == stackData.stackOwnerName);
                if (stack != null)
                {
                    stack.SetCount(stackData.documentCount);
                }
            }

            PlayerPrefs.SetInt("LastUsedSlot", slotIndex);
            Debug.Log($"Игра загружена из слота {slotIndex}");
            return true;
        }
        
        Debug.LogWarning($"Файл сохранения для слота {slotIndex} не найден!");
        return false;
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
		isNewGame = true;
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