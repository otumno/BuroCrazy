// Assets/Scripts/Managers/SaveLoadManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Gameplay; // Нужно для доступа к OfficeObjectDurability

namespace Managers
{
    public class SaveLoadManager : MonoBehaviour
    {
        public static SaveLoadManager Instance { get; set; }

        [Tooltip("Сколько слотов сохранения будет в игре")]
        public int numberOfSlots = 3;
        public bool isNewGame = true;
        private int currentSlotIndex = 0;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void SaveGame(int slotIndex)
        {
            isNewGame = false;
            currentSlotIndex = slotIndex;
            SaveData data = new SaveData();

            // 1. Глобальные счетчики
            data.day = CalendarManager.Instance.CurrentDay;
            data.money = PlayerWallet.Instance.GetCurrentMoney();
            data.archiveDocumentCount = ArchiveManager.Instance.GetCurrentDocumentCount();

            // 2. Приказы
            if (OrderManager.Instance != null)
            {
                data.activePermanentOrderNames = OrderManager.Instance.activePermanentOrders.Select(order => order.name).ToList();
                data.completedOneTimeOrderNames = OrderManager.Instance.completedOneTimeOrders.Select(order => order.name).ToList();
            }

            // 3. Персонал
            data.allStaffData = new List<StaffSaveData>();
            StaffController[] allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
            foreach (var staffMember in allStaff)
            {
                StaffSaveData staffData = new StaffSaveData();
                staffData.gameObjectName = staffMember.gameObject.name;
                staffData.nameData = staffMember.nameData;
                staffData.position = staffMember.transform.position;
                staffData.stressLevel = staffMember.GetCurrentFrustration();
            
                staffData.assignedWorkstationId = staffMember.assignedWorkstation != null ? staffMember.assignedWorkstation.deskId : -999;
                staffData.scheduleTrackIndex = staffMember.uiScheduleTrackIndex;
                
                // Сохраняем статистику посещаемости
                staffData.totalLatenessCount = staffMember.totalLatenessCount;
                staffData.sickDaysCount = staffMember.sickDaysCount;
                
                // Сохраняем особенность (trait)
                staffData.trait = staffMember.permanentTrait;
                
                // Сохраняем навыки и роль (если нужно глубокое сохранение, добавьте сюда поля из StaffController)
                // staffData.role = staffMember.currentRole;
                
                data.allStaffData.Add(staffData);
            }

            // 4. Стопки документов
            data.allDocumentStackData = new List<DocumentStackSaveData>();
            DocumentStack[] allStacks = FindObjectsByType<DocumentStack>(FindObjectsSortMode.None);
            foreach (var stack in allStacks)
            {
                // Пропускаем архив, он сохраняется отдельно
                if (ArchiveManager.Instance != null && stack == ArchiveManager.Instance.mainDocumentStack) continue;
                
                DocumentStackSaveData stackData = new DocumentStackSaveData();
                stackData.stackOwnerName = stack.gameObject.name;
                stackData.documentCount = stack.CurrentSize;
                data.allDocumentStackData.Add(stackData);
            }
            
            // 5. Сюжет
            if (StoryStateManager.Instance != null)
            {
                StoryStateManager.Instance.SaveToData(data);
            }

            // 6. [НОВОЕ] Прочность объектов (Durability)
            data.allDurabilityData = new List<DurabilitySaveData>();
            var allDurables = FindObjectsByType<OfficeObjectDurability>(FindObjectsSortMode.None);
            
            foreach (var item in allDurables)
            {
                DurabilitySaveData dData = new DurabilitySaveData();
                dData.objectName = item.gameObject.name;
                dData.position = item.transform.position;
                dData.currentHealth = item.currentHealth;
                
                data.allDurabilityData.Add(dData);
            }

            // 7. Контакты телефона
            if (PhoneManager.Instance != null)
            {
                data.unlockedContactIDs = PhoneManager.Instance.GetUnlockedContactIDs();
            }

            // Запись на диск
            WriteSaveDataToFile(slotIndex, data);
            PlayerPrefs.SetInt("LastUsedSlot", slotIndex);
            // Debug.Log($"Игра сохранена в слот {slotIndex}");
        }

        public void SaveNewGame(int slotIndex, SaveData initialData)
        {
            isNewGame = false;
            currentSlotIndex = slotIndex;
            WriteSaveDataToFile(slotIndex, initialData);
            
            if (StoryStateManager.Instance != null)
            {
                StoryStateManager.Instance.ResetState();
            }
            
            PlayerPrefs.SetInt("LastUsedSlot", slotIndex);
            // Debug.Log($"Новая игра создана и сохранена в слот {slotIndex}");
        }
    
        private void WriteSaveDataToFile(int slotIndex, SaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
            File.WriteAllText(path, json);
        }
    
        public void SetCurrentSlot(int slotIndex)
        {
            currentSlotIndex = slotIndex;
            // Debug.Log($"[SaveLoadManager] Текущий слот изменен на {slotIndex}");
        }
    
        public int GetCurrentSlot()
        {
            return currentSlotIndex;
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

                // 1. Восстановление глобальных данных
                CalendarManager.Instance.SetDay(data.day);
                PlayerWallet.Instance.SetMoney(data.money);
                ArchiveManager.Instance.SetDocumentCount(data.archiveDocumentCount);
                
                if (StoryStateManager.Instance != null)
                {
                    StoryStateManager.Instance.LoadFromData(data);
                }

                // 2. Восстановление приказов
                if (OrderManager.Instance != null)
                {
                    var allOrders = OrderManager.Instance.allPossibleOrders;
                    OrderManager.Instance.activePermanentOrders.Clear();
                    OrderManager.Instance.completedOneTimeOrders.Clear();

                    if (data.activePermanentOrderNames != null)
                    {
                        foreach (string orderName in data.activePermanentOrderNames)
                        {
                            DirectorOrder orderAsset = allOrders.FirstOrDefault(o => o.name == orderName);
                            if (orderAsset != null)
                            {
                                OrderManager.Instance.activePermanentOrders.Add(orderAsset);
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
                                OrderManager.Instance.completedOneTimeOrders.Add(orderAsset);
                            }
                        }
                    }
                }

                // 3. Восстановление персонала
                StaffController[] allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
                foreach (var staffData in data.allStaffData)
                {
                    StaffController staffMember = allStaff.FirstOrDefault(s => s.gameObject.name == staffData.gameObjectName);
                    if (staffMember != null)
                    {
                        staffMember.transform.position = staffData.position;
                        staffMember.SetCurrentFrustration(staffData.stressLevel);
                        staffMember.nameData = staffData.nameData;
                        
                        // Загружаем статистику посещаемости
                        staffMember.totalLatenessCount = staffData.totalLatenessCount;
                        staffMember.sickDaysCount = staffData.sickDaysCount;
                        
                        // Загружаем особенность (trait)
                        staffMember.permanentTrait = staffData.trait;
                    
                        if (staffData.assignedWorkstationId != -999)
                        {
                            var workstation = ScenePointsRegistry.Instance.GetServicePointByID(staffData.assignedWorkstationId);
                            if (workstation != null)
                            {
                                AssignmentManager.Instance.AssignStaffToWorkstation(staffMember, workstation);
                            }
                        }
                    }
                }

                // 4. Восстановление документов
                DocumentStack[] allStacks = FindObjectsByType<DocumentStack>(FindObjectsSortMode.None);
                foreach (var stackData in data.allDocumentStackData)
                {
                    DocumentStack stack = allStacks.FirstOrDefault(s => s.gameObject.name == stackData.stackOwnerName);
                    if (stack != null)
                    {
                        stack.SetCount(stackData.documentCount);
                    }
                }

                // 5. [НОВОЕ] Восстановление прочности объектов
                if (data.allDurabilityData != null)
                {
                    var allDurables = FindObjectsByType<OfficeObjectDurability>(FindObjectsSortMode.None);
                    
                    foreach (var dData in data.allDurabilityData)
                    {
                        // Ищем объект по имени
                        var targetObj = allDurables.FirstOrDefault(d => d.gameObject.name == dData.objectName);
                        
                        if (targetObj != null)
                        {
                            targetObj.currentHealth = dData.currentHealth;
                            
                            // Вызываем публичный метод для обновления визуалов
                            targetObj.CheckState(); 
                        }
                    }
                }

                // 6. Восстановление контактов телефона
                if (PhoneManager.Instance != null && data.unlockedContactIDs != null)
                {
                    PhoneManager.Instance.LoadUnlockedContacts(data.unlockedContactIDs);
                }

                PlayerPrefs.SetInt("LastUsedSlot", slotIndex);
                Debug.Log($"Игра загружена из слота {slotIndex}");
                return true;
            }
        
            Debug.LogWarning($"Файл сохранения для слота {slotIndex} не найден!");
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
                // Debug.Log($"Сохранение в слоте {slotIndex} удалено.");
            }
        }

        public bool DoesSaveExist(int slotIndex)
        {
            string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
            return File.Exists(path);
        }
    
        public bool DoesAnySaveExist()
        {
            for (int i = 0; i < numberOfSlots; i++)
            {
                if (DoesSaveExist(i))
                {
                    return true;
                }
            }
            return false;
        }
    
        public int GetLatestSaveSlotIndex()
        {
            int latestSlot = -1;
            DateTime latestTime = DateTime.MinValue;

            for (int i = 0; i < numberOfSlots; i++)
            {
                string path = Path.Combine(Application.persistentDataPath, $"save_slot_{i}.json");
                if (File.Exists(path))
                {
                    DateTime writeTime = File.GetLastWriteTime(path);
                    if (writeTime > latestTime)
                    {
                        latestTime = writeTime;
                        latestSlot = i;
                    }
                }
            }
            return latestSlot;
        }
    
        public bool IsSlotEmpty(int slotIndex)
        {
            return !DoesSaveExist(slotIndex);
        }
    }
}