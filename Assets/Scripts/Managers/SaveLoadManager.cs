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
        // Флаг «нужна инициализация новой игры». В отличие от isNewGame, НЕ сбрасывается в
        // SaveNewGame/LoadGame — иначе к моменту проверки в MainUIManager.UnveilSequence он всегда
        // false, и настройки новой игры (ресеты менеджеров, арки, создание директора) не применяются.
        public bool pendingNewGameSetup = false;
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
            // Берём за основу текущий файл слота, а не пустой SaveData: часть полей живёт только в файле
            // (gameCompleted, код создания директора и т.п.) и ни один менеджер
            // их не держит — с CreateEmpty() каждое сохранение молча сбрасывало их в значения по умолчанию.
            // Всё, что собирается ниже, перезаписывается целиком (списки — новыми списками).
            SaveData data = GetDataForSlot(slotIndex) ?? SaveData.CreateEmpty();

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
            // Берём список из HiringManager, а не FindObjectsByType: сотрудники вне смены выключены (сидят дома)
            // и поиском по сцене не находятся. Временных тоже сохраняем: их смена оплачена вперёд.
            data.allStaffData = new List<StaffSaveData>();
            if (HiringManager.Instance != null)
            {
                foreach (var staffMember in HiringManager.Instance.AllStaff)
                {
                    if (staffMember == null) continue;
                    data.allStaffData.Add(CaptureStaff(staffMember, isHired: true));
                }
            }

            // Директор лежит в сцене — его, как и раньше, только находим по имени и обновляем.
            if (DirectorAvatarController.Instance != null)
            {
                data.allStaffData.Add(CaptureStaff(DirectorAvatarController.Instance, isHired: false));
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

            // 7.0. Доигранные события (катсцены, в том числе туториал)
            if (CinematicSystem.CinematicTriggerManager.Instance != null)
            {
                data.cinematicTriggerStates = CinematicSystem.CinematicTriggerManager.Instance.GetTriggerStates();
            }

            // 7.1. Открытые должности Директора (от них зависит, кого можно нанимать)
            if (ProgressionManager.Instance != null)
            {
                data.unlockedJobIDs = ProgressionManager.Instance.GetUnlockedJobIDsForSave();
            }

            // 8. [НОВОЕ] Прогресс сюжетных арок
            if (StorySystem.ArcManager.Instance != null)
            {
                data.arcProgress = StorySystem.ArcManager.Instance.GetArcProgress();
            }

            // 9. [НОВОЕ] Черты личности Директора
            if (TraitManager.Instance != null)
            {
                TraitManager.Instance.Save(ref data);
            }

            // 10. Пол Директора (выбран в книге создания, влияет на внешность и портрет)
            if (DirectorAvatarController.Instance != null)
            {
                data.gender = DirectorAvatarController.Instance.gender;
            }

            // Запись на диск
            WriteSaveDataToFile(slotIndex, data);
            PlayerPrefs.SetInt("LastUsedSlot", slotIndex);
            // Debug.Log($"Игра сохранена в слот {slotIndex}");
        }

        private static StaffSaveData CaptureStaff(StaffController staffMember, bool isHired)
        {
            StaffSaveData staffData = new StaffSaveData();
            staffData.isHired = isHired;
            staffData.gameObjectName = staffMember.gameObject.name;
            staffData.nameData = staffMember.nameData;
            staffData.position = staffMember.transform.position;
            staffData.stressLevel = staffMember.GetCurrentFrustration();

            staffData.role = staffMember.currentRole;
            staffData.gender = staffMember.gender;
            staffData.salary = staffMember.salaryPerPeriod;
            staffData.experience = staffMember.experiencePoints;
            staffData.rankName = staffMember.currentRank != null ? staffMember.currentRank.name : "";
            staffData.employmentType = staffMember.employmentType;
            staffData.activeActionNames = staffMember.activeActions != null
                ? staffMember.activeActions.Where(a => a != null).Select(a => a.name).ToList()
                : new List<string>();

            if (staffMember.skills != null)
            {
                staffData.paperworkMastery = staffMember.skills.paperworkMastery;
                staffData.sedentaryResilience = staffMember.skills.sedentaryResilience;
                staffData.pedantry = staffMember.skills.pedantry;
                staffData.softSkills = staffMember.skills.softSkills;
                staffData.corruption = staffMember.skills.corruption;
                staffData.dirtyHands = staffMember.skills.dirtyHands;
            }

            staffData.assignedWorkstationId = staffMember.assignedWorkstation != null ? staffMember.assignedWorkstation.deskId : -999;
            staffData.scheduleTrackIndex = staffMember.uiScheduleTrackIndex;
            staffData.workShiftMask = staffMember.WorkShiftMask;

            staffData.unpaidPeriods = staffMember.unpaidPeriods;
            staffData.missedPaymentCount = staffMember.missedPaymentCount;

            // Статистика посещаемости
            staffData.totalLatenessCount = staffMember.totalLatenessCount;
            staffData.sickDaysCount = staffMember.sickDaysCount;

            // Особенность (trait)
            staffData.trait = staffMember.permanentTrait;

            return staffData;
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
                // Нанятых пересоздаём, остальных (директор, записи старых сейвов) ищем по имени.
                // Сначала убираем текущий штат: LoadGame вызывают и поверх идущей сцены (перезагрузка после отстранения).
                HiringManager.Instance?.DestroyAllStaff();
                StaffController[] allStaff = FindObjectsByType<StaffController>(FindObjectsSortMode.None);
                foreach (var staffData in data.allStaffData ?? new List<StaffSaveData>())
                {
                    StaffController staffMember = staffData.isHired
                        ? HiringManager.Instance?.RestoreStaff(staffData)
                        : allStaff.FirstOrDefault(s => s.gameObject.name == staffData.gameObjectName);
                    if (staffMember != null)
                    {
                        // Нанятые стартуют дома (их выводит на смену менеджер смен), позицию берём только у объектов сцены.
                        if (!staffData.isHired) staffMember.transform.position = staffData.position;
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

                // Если загрузились посреди дня, выводим на смену тех, чей период уже идёт, не дожидаясь следующего.
                HiringManager.Instance?.CheckAllStaffShiftsImmediately();

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

                // 6.0. Доигранные события — до того, как MainUIManager запустит события начала дня
                if (CinematicSystem.CinematicTriggerManager.Instance != null)
                {
                    CinematicSystem.CinematicTriggerManager.Instance.RestoreTriggerStates(data.cinematicTriggerStates);
                }

                // 6.1. Открытые должности Директора
                if (ProgressionManager.Instance != null)
                {
                    ProgressionManager.Instance.LoadUnlockedJobIDs(data.unlockedJobIDs);
                }

                // 7. [НОВОЕ] Восстановление прогресса сюжетных арок
                if (StorySystem.ArcManager.Instance != null)
                {
                    StorySystem.ArcManager.Instance.LoadArcProgress(data.arcProgress);
                }

                // 8. [НОВОЕ] Восстановление черт личности Директора
                if (TraitManager.Instance != null)
                {
                    TraitManager.Instance.Load(data);
                }

                // 9. Пол Директора. Директор лежит в GameScene префабом с полом по умолчанию,
                //    поэтому пол из сейва накатываем поверх и перерисовываем внешность.
                if (DirectorAvatarController.Instance != null)
                {
                    DirectorAvatarController.Instance.ApplyAppearance(data.gender);
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