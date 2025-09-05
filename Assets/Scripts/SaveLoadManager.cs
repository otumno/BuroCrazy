using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SaveLoadManager : MonoBehaviour
{
    public static SaveLoadManager Instance { get; private set; }
    private int currentSlotIndex = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; DontDestroyOnLoad(gameObject); }
    }

    public void SaveGame(int slotIndex)
    {
        currentSlotIndex = slotIndex;
        SaveData data = new SaveData();
        
        data.day = ClientSpawner.Instance.GetCurrentDay();
        data.money = PlayerWallet.Instance.GetCurrentMoney();
        data.archiveDocumentCount = ArchiveManager.Instance.GetCurrentDocumentCount();

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

    public bool LoadGame(int slotIndex)
    {
        string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");

        if (File.Exists(path))
        {
            currentSlotIndex = slotIndex;
            string json = File.ReadAllText(path);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            ClientSpawner.Instance.SetDay(data.day);
            PlayerWallet.Instance.SetMoney(data.money);
            ArchiveManager.Instance.SetDocumentCount(data.archiveDocumentCount);

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