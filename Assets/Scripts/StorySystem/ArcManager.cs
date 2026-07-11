using System.Collections.Generic;
using UnityEngine;
using Characters;
using DialogueSystem.Data;
using Data.Calendar;
using Managers;

namespace StorySystem
{
    /// <summary>
    /// Менеджер сюжетных арок: выбирает арки для прохождения, планирует этапы по дням,
    /// уведомляет WaveManager о необходимости спавна арковых посетителей.
    /// </summary>
    public class ArcManager : MonoBehaviour
    {
        public static ArcManager Instance { get; private set; }

        [Header("Настройки")]
        [SerializeField] private int maxActiveArcs = 3;
        [SerializeField] private int maxEncountersPerDay = 3;
        [SerializeField] private int totalGameDays = 30;

        [Header("Выбор арок")]
        [Tooltip("Минимальное число арок для прохождения.")]
        [SerializeField] private int minArcsPerPlaythrough = 4;
        [Tooltip("Максимальное число арок для прохождения.")]
        [SerializeField] private int maxArcsPerPlaythrough = 7;

        [Header("PlayerPrefs")]
        [SerializeField] private string completedArcsPrefsKey = "CompletedArcs";

        [Header("Отладка (Read Only)")]
        [SerializeField] private List<string> debugAllArcIDs = new List<string>();
        [SerializeField] private List<string> debugActiveArcs = new List<string>();
        [SerializeField] private int debugQueuedEncounters = 0;

        // --- Внутренние данные ---
        private List<ArcDefinition> allArcDefinitions = new List<ArcDefinition>();
        private List<ArcInstance> activeArcs = new List<ArcInstance>();
        private List<ArcEncounter> plannedEncounters = new List<ArcEncounter>();
        private HashSet<string> completedArcsGlobal = new HashSet<string>();

        private bool _isInitialized = false;

        // -------------------------------------------------------------------------
        // UNITY LIFECYCLE
        // -------------------------------------------------------------------------

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // Обеспечиваем выживание между загрузками сцен (если висит на [SYSTEMS])
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            LoadAllArcDefinitions();
            LoadCompletedArcs();
        }

        private void Start()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayChanged += OnDayChanged;
            }
            else
            {
                Debug.LogWarning("[ArcManager] TimeManager.Instance не найден. Подписка невозможна.");
            }

            // Подписка на событие диалога. Если DialogueUIManager ещё не готов —
            // сделаем отложенную попытку через несколько кадров.
            TrySubscribeToDialogue();
        }

        private void TrySubscribeToDialogue()
        {
            if (DialogueUIManager.Instance != null)
            {
                DialogueUIManager.Instance.OnDialogueFinished -= OnDialogueFinished;
                DialogueUIManager.Instance.OnDialogueFinished += OnDialogueFinished;
                return;
            }
            StartCoroutine(WaitAndSubscribeToDialogue());
        }

        private System.Collections.IEnumerator WaitAndSubscribeToDialogue()
        {
            int safety = 60;
            while (DialogueUIManager.Instance == null && safety-- > 0)
            {
                yield return null;
            }

            if (DialogueUIManager.Instance != null)
            {
                DialogueUIManager.Instance.OnDialogueFinished -= OnDialogueFinished;
                DialogueUIManager.Instance.OnDialogueFinished += OnDialogueFinished;
            }
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayChanged -= OnDayChanged;
            }
            if (DialogueUIManager.Instance != null)
            {
                DialogueUIManager.Instance.OnDialogueFinished -= OnDialogueFinished;
            }

            if (Instance == this) Instance = null;
        }

        // -------------------------------------------------------------------------
        // ИНИЦИАЛИЗАЦИЯ И ВЫБОР АРОК
        // -------------------------------------------------------------------------

        /// <summary>
        /// Загружает все ArcDefinition из Resources/Arcs.
        /// </summary>
        private void LoadAllArcDefinitions()
        {
            allArcDefinitions.Clear();
            debugAllArcIDs.Clear();

            var loaded = Resources.LoadAll<ArcDefinition>("Arcs");
            foreach (var arc in loaded)
            {
                if (arc == null) continue;
                if (string.IsNullOrEmpty(arc.arcID))
                {
                    Debug.LogWarning($"[ArcManager] ArcDefinition '{arc.name}' имеет пустой arcID — пропускаем.");
                    continue;
                }
                if (arc.stages == null || arc.stages.Count == 0)
                {
                    Debug.LogWarning($"[ArcManager] ArcDefinition '{arc.arcID}' не имеет этапов — пропускаем.");
                    continue;
                }
                allArcDefinitions.Add(arc);
                debugAllArcIDs.Add(arc.arcID);
            }

            Debug.Log($"[ArcManager] Загружено {allArcDefinitions.Count} арок из Resources/Arcs");
        }

        /// <summary>
        /// Вызывается при старте новой игры или загрузке сохранения.
        /// </summary>
        public void Initialize()
        {
            if (allArcDefinitions.Count == 0)
            {
                Debug.Log("[ArcManager] Нет загруженных арок. ArcManager работает в холостом режиме.");
                _isInitialized = true;
                return;
            }

            activeArcs.Clear();
            plannedEncounters.Clear();

            if (TimeManager.Instance == null)
            {
                Debug.LogWarning("[ArcManager] Initialize: TimeManager.Instance отсутствует.");
                return;
            }

            int currentDay = TimeManager.Instance.GetCurrentDay();
            SelectArcsForPlaythrough(currentDay);

            _isInitialized = true;

            // Сразу обработать текущий день: первый этап может активироваться в день старта
            OnDayChanged(currentDay);

            RefreshDebugInfo();
        }

        /// <summary>
        /// Выбирает арки для текущего прохождения.
        /// </summary>
        private void SelectArcsForPlaythrough(int currentDay)
        {
            // 1. Фильтрация: не сыгранные ранее (если isOneTimeOnly)
            var candidates = new List<ArcDefinition>();
            foreach (var arc in allArcDefinitions)
            {
                if (arc == null) continue;

                if (arc.isOneTimeOnly && completedArcsGlobal.Contains(arc.arcID))
                    continue;

                // 2. Проверка, что арка укладывается в оставшееся время
                if (currentDay + arc.durationDays > totalGameDays)
                    continue;

                candidates.Add(arc);
            }

            if (candidates.Count == 0)
            {
                Debug.Log("[ArcManager] Нет доступных арок после фильтрации.");
                return;
            }

            // 3. Сортировка по приоритету (убывание), затем перемешивание внутри групп
            candidates.Sort((a, b) => b.priority.CompareTo(a.priority));

            // Перемешивание с учётом приоритета: арки с большим priority имеют больше шансов
            var weighted = new List<(ArcDefinition arc, float weight)>();
            foreach (var arc in candidates)
            {
                float weight = Mathf.Max(0.1f, arc.priority);
                weighted.Add((arc, weight));
            }

            // 4. Выбор количества арок
            int maxCount = Mathf.Min(maxArcsPerPlaythrough, candidates.Count, maxActiveArcs);
            int minCount = Mathf.Min(minArcsPerPlaythrough, maxCount);
            int countToSelect = Random.Range(minCount, maxCount + 1);

            // Жадный отбор по весу
            var selected = new List<ArcDefinition>();
            for (int i = 0; i < countToSelect; i++)
            {
                if (weighted.Count == 0) break;

                float totalWeight = 0f;
                foreach (var w in weighted) totalWeight += w.weight;

                float roll = Random.value * totalWeight;
                float cursor = 0f;
                int chosenIndex = -1;
                for (int j = 0; j < weighted.Count; j++)
                {
                    cursor += weighted[j].weight;
                    if (roll <= cursor)
                    {
                        chosenIndex = j;
                        break;
                    }
                }

                if (chosenIndex < 0) chosenIndex = weighted.Count - 1;
                selected.Add(weighted[chosenIndex].arc);
                weighted.RemoveAt(chosenIndex);
            }

            // 5. Создание ArcInstance
            foreach (var arc in selected)
            {
                var instance = new ArcInstance(arc, currentDay);
                activeArcs.Add(instance);
                Debug.Log($"[ArcManager] Выбрана арка '{arc.arcID}' (приоритет {arc.priority}, длительность {arc.durationDays} дней, {arc.stages.Count} этапов)");
            }
        }

        // -------------------------------------------------------------------------
        // ОБРАБОТКА СМЕНЫ ДНЯ
        // -------------------------------------------------------------------------

        private void OnDayChanged(int newDay)
        {
            if (!_isInitialized) return;

            // 1. Удалить завершённые арки
            for (int i = activeArcs.Count - 1; i >= 0; i--)
            {
                if (activeArcs[i].isCompleted)
                {
                    Debug.Log($"[ArcManager] Арка '{activeArcs[i].definition.arcID}' завершена и удалена из активных.");
                    activeArcs.RemoveAt(i);
                }
            }

            // 2. Для каждой активной арки проверить готовность следующего этапа
            int remainingEncounterSlots = maxEncountersPerDay;

            // Сначала обработаем уже запланированные встречи
            remainingEncounterSlots -= plannedEncounters.Count;

            // 3. Активация этапов
            foreach (var arc in activeArcs)
            {
                if (arc.isCompleted || !arc.HasMoreStages) continue;

                int stageIndex = arc.currentStageIndex;
                if (stageIndex >= arc.stageActivated.Length) continue;

                // Если этап уже активирован ранее — пропускаем (на случай, если остался со старого дня)
                if (arc.stageActivated[stageIndex]) continue;

                var stage = arc.definition.stages[stageIndex];
                if (stage == null) continue;

                // Проверяем, что день уже наступил
                int arcDay = newDay - arc.startDay;
                if (arcDay < stage.dayOffset) continue;

                // Проверяем условие флага
                if (!string.IsNullOrEmpty(stage.requiredFlag))
                {
                    if (StoryStateManager.Instance == null ||
                        StoryStateManager.Instance.GetFlag(stage.requiredFlag) != 1)
                    {
                        // Условие не выполнено — пропускаем в этом дне
                        continue;
                    }
                }

                // Пытаемся активировать
                if (remainingEncounterSlots <= 0)
                {
                    // Нет слотов — отложим на следующий день
                    continue;
                }

                if (TryActivateStage(arc, stageIndex, newDay))
                {
                    remainingEncounterSlots--;
                }
            }

            RefreshDebugInfo();

            // 4. Запланировать запланированные встречи на сегодня
            ProcessPlannedEncounters(newDay);
        }

        /// <summary>
        /// Пытается активировать этап арки: ставит onStartFlag и создаёт запись в очереди.
        /// </summary>
        private bool TryActivateStage(ArcInstance arc, int stageIndex, int scheduledDay)
        {
            if (arc == null || arc.definition == null) return false;
            if (stageIndex < 0 || stageIndex >= arc.definition.stages.Count) return false;

            var stage = arc.definition.stages[stageIndex];

            // Установить onStartFlag
            if (!string.IsNullOrEmpty(stage.onStartFlag))
            {
                if (StoryStateManager.Instance != null)
                {
                    StoryStateManager.Instance.SetFlag(stage.onStartFlag, 1);
                }
            }

            // Создать запись о встрече
            var encounter = new ArcEncounter
            {
                arc = arc,
                stageIndex = stageIndex,
                dialogue = stage.dialogue,
                goal = arc.definition.GetGoalForStage(stageIndex),
                archetype = arc.definition.GetArchetypeForStage(stageIndex),
                isRemote = stage.isRemoteInteraction,
                scheduledDay = scheduledDay,
                characterName = stage.characterName
            };

            plannedEncounters.Add(encounter);
            arc.stageActivated[stageIndex] = true;

            Debug.Log($"[ArcManager] Активирован этап {stageIndex} арки '{arc.definition.arcID}' (запланировано на день {scheduledDay})");
            return true;
        }

        /// <summary>
        /// Обрабатывает запланированные встречи: при ночи переносит их на следующий день,
        /// иначе регистрирует посетителя в WaveManager на текущий день.
        /// </summary>
        private void ProcessPlannedEncounters(int currentDay)
        {
            if (plannedEncounters.Count == 0) return;

            // Проверяем, можно ли сейчас спавнить (день, не ночь и не вечер)
            if (!CanSpawnDuringCurrentPeriod())
            {
                // Переносим на следующий день
                foreach (var enc in plannedEncounters)
                {
                    enc.scheduledDay = currentDay + 1;
                }
                Debug.Log($"[ArcManager] {plannedEncounters.Count} встреч отложено на день {currentDay + 1} (неподходящий период).");
                RefreshDebugInfo();
                return;
            }

            // Регистрируем каждую встречу в WaveManager
            for (int i = plannedEncounters.Count - 1; i >= 0; i--)
            {
                var enc = plannedEncounters[i];

                // Если scheduledDay ушёл вперёд (например, после загрузки), пропускаем и оставляем
                if (enc.scheduledDay < currentDay) enc.scheduledDay = currentDay;

                if (WaveManager.Instance != null)
                {
                    WaveManager.Instance.AddArcVisitor(
                        enc.dialogue,
                        enc.goal,
                        enc.archetype,
                        enc.scheduledDay,
                        enc.isRemote,
                        enc.characterName
                    );
                }
                else
                {
                    Debug.LogWarning("[ArcManager] WaveManager.Instance не найден при обработке встреч.");
                }

                plannedEncounters.RemoveAt(i);
            }

            RefreshDebugInfo();
        }

        /// <summary>
        /// Можно ли сейчас спавнить клиента исходя из текущего периода.
        /// </summary>
        private bool CanSpawnDuringCurrentPeriod()
        {
            if (TimeManager.Instance == null) return false;

            if (TimeManager.Instance.IsNight())
                return false;

            var period = TimeManager.Instance.GetCurrentPeriodType();
            // Исключаем последний период вечера и ночи
            if (period == CalendarDayPeriodType.Evening)
                return false;

            return true;
        }

        // -------------------------------------------------------------------------
        // ОБРАБОТКА ЗАВЕРШЕНИЯ ДИАЛОГА
        // -------------------------------------------------------------------------

        private void OnDialogueFinished(DialogueGraph finishedDialogue)
        {
            if (finishedDialogue == null) return;

            // Ищем арку, которой принадлежит этот диалог
            for (int i = activeArcs.Count - 1; i >= 0; i--)
            {
                var arc = activeArcs[i];
                if (arc == null || arc.definition == null) continue;
                if (arc.isCompleted) continue;

                // Проверяем текущий этап
                if (arc.currentStageIndex >= 0 && arc.currentStageIndex < arc.definition.stages.Count)
                {
                    var stage = arc.definition.stages[arc.currentStageIndex];
                    if (stage == null) continue;

                    if (stage.dialogue == finishedDialogue)
                    {
                        AdvanceArc(arc);
                        break;
                    }
                }

                // Дополнительная проверка: может быть это любой из этапов (на случай, если currentStageIndex сбился)
                for (int s = 0; s < arc.definition.stages.Count; s++)
                {
                    if (arc.stageActivated[s] &&
                        arc.definition.stages[s].dialogue == finishedDialogue)
                    {
                        if (s == arc.currentStageIndex)
                        {
                            // Уже обработано выше
                            break;
                        }
                        // Синхронизируем currentStageIndex
                        arc.currentStageIndex = s;
                        AdvanceArc(arc);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Переводит арку на следующий этап или помечает как завершённую.
        /// </summary>
        private void AdvanceArc(ArcInstance arc)
        {
            if (arc == null || arc.definition == null) return;

            var stage = arc.definition.stages[arc.currentStageIndex];
            if (stage != null && !string.IsNullOrEmpty(stage.onCompleteFlag))
            {
                // Убедимся, что флаг установлен (даже если диалог его не выставил)
                if (StoryStateManager.Instance != null &&
                    StoryStateManager.Instance.GetFlag(stage.onCompleteFlag) != 1)
                {
                    StoryStateManager.Instance.SetFlag(stage.onCompleteFlag, 1);
                }
            }

            arc.currentStageIndex++;
            Debug.Log($"[ArcManager] Арка '{arc.definition.arcID}' перешла на этап {arc.currentStageIndex}");

            if (!arc.HasMoreStages)
            {
                CompleteArc(arc);
            }

            RefreshDebugInfo();
        }

        private void CompleteArc(ArcInstance arc)
        {
            arc.isCompleted = true;
            if (arc.definition.isOneTimeOnly && !string.IsNullOrEmpty(arc.definition.arcID))
            {
                completedArcsGlobal.Add(arc.definition.arcID);
                SaveCompletedArcs();
            }
            Debug.Log($"[ArcManager] Арка '{arc.definition.arcID}' полностью завершена.");
        }

        // -------------------------------------------------------------------------
        // НЕМЕДЛЕННЫЙ СПАВН (для дебаг-меню и для случаев "арка-в-этот-день")
        // -------------------------------------------------------------------------

        /// <summary>
        /// Находит первую активную арку с заданным ID (если указан) и спавнит
        /// посетителя её текущего этапа прямо сейчас. Используется дебаг-меню.
        /// </summary>
        /// <param name="arcID">ID арки, или null/пусто — тогда берётся первая арка с незавершённым этапом.</param>
        /// <returns>true если клиент был заспавнен, false если подходящей арки не нашлось.</returns>
        public bool SpawnActiveStageVisitorNow(string arcID = null)
        {
            ArcInstance target = null;
            int stageIndex = -1;
            foreach (var arc in activeArcs)
            {
                if (arc == null || arc.definition == null || arc.isCompleted) continue;
                if (arc.currentStageIndex < 0 || arc.currentStageIndex >= arc.definition.stages.Count) continue;

                if (!string.IsNullOrEmpty(arcID) && arc.definition.arcID != arcID) continue;

                target = arc;
                stageIndex = arc.currentStageIndex;
                if (string.IsNullOrEmpty(arcID)) break;
            }

            if (target == null || stageIndex < 0)
            {
                Debug.LogWarning($"[ArcManager] SpawnActiveStageVisitorNow: нет активной арки{(string.IsNullOrEmpty(arcID) ? "" : $" '{arcID}'")} со свободным этапом.");
                return false;
            }

            var stage = target.definition.stages[stageIndex];
            if (stage == null || stage.dialogue == null)
            {
                Debug.LogWarning($"[ArcManager] SpawnActiveStageVisitorNow: у арки '{target.definition.arcID}' этап {stageIndex} без диалога.");
                return false;
            }

            SpawnStageVisitorNow(target, stageIndex, stage);
            return true;
        }

        /// <summary>
        /// Принудительно спавнит клиента для конкретного этапа конкретной арки прямо сейчас.
        /// Использует <see cref="WaveManager.SpawnSpecialClient"/> или, если менеджер недоступен,
        /// создаёт ScheduledVisitor в очередь.
        /// </summary>
        private void SpawnStageVisitorNow(ArcInstance arc, int stageIndex, StageDefinition stage)
        {
            if (arc == null || stage == null) return;
            if (stage.dialogue == null) return;

            ClientGoal goal = arc.definition.GetGoalForStage(stageIndex);
            ClientArchetype archetype = arc.definition.GetArchetypeForStage(stageIndex);
            string characterName = stage.characterName;
            string arcID = arc.definition.arcID;

            // [ИСПРАВЛЕНО] Пол берём явно из этапа. Если в JSON указано "Any" или пусто,
            // ParseGenderString возвращает Gender.Male по умолчанию (для совместимости).
            // Арковые сюжеты могут указать "Female" — клиент будет визуально женским,
            // потому что ApplyPendingClientOverrides применит Gender ДО клик-инициализации визуала.
            Enums.Gender characterGender = arc.definition.GetGenderForStage(stageIndex);

            if (WaveManager.Instance == null)
            {
                Debug.LogWarning("[ArcManager] SpawnStageVisitorNow: WaveManager.Instance отсутствует — арковый клиент не заспавнен.");
                return;
            }

            // Немедленный спавн: обходит дневной гейтинг ProcessArcVisitors.
            int spawned = WaveManager.Instance.SpawnArcClientNow(
                stage.dialogue,
                goal,
                archetype,
                characterName,
                characterGender);

            if (spawned > 0)
            {
                // Помечаем этап как активированный, чтобы естественный flow не дублировал.
                if (stageIndex >= 0 && stageIndex < arc.stageActivated.Length && !arc.stageActivated[stageIndex])
                {
                    arc.stageActivated[stageIndex] = true;
                }
                Debug.Log($"[ArcManager] SpawnStageVisitorNow: '{arcID}' этап {stageIndex} немедленно заспавнен.");
            }
        }

        // -------------------------------------------------------------------------
        // СОХРАНЕНИЕ / ЗАГРУЗКА ГЛОБАЛЬНОГО ПРОГРЕССА
        // -------------------------------------------------------------------------

        private void SaveCompletedArcs()
        {
            string joined = string.Join(",", completedArcsGlobal);
            PlayerPrefs.SetString(completedArcsPrefsKey, joined);
            PlayerPrefs.Save();
        }

        private void LoadCompletedArcs()
        {
            completedArcsGlobal.Clear();
            string joined = PlayerPrefs.GetString(completedArcsPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(joined)) return;

            string[] parts = joined.Split(',');
            foreach (var p in parts)
            {
                if (!string.IsNullOrEmpty(p))
                    completedArcsGlobal.Add(p);
            }
            Debug.Log($"[ArcManager] Загружено {completedArcsGlobal.Count} глобально завершённых арок.");
        }

        /// <summary>
        /// Сбрасывает глобальный прогресс арок (для отладки/новой игры с чистого листа).
        /// </summary>
        public void ResetGlobalProgress()
        {
            completedArcsGlobal.Clear();
            PlayerPrefs.DeleteKey(completedArcsPrefsKey);
            PlayerPrefs.Save();
            Debug.Log("[ArcManager] Глобальный прогресс арок сброшен.");
        }

        // -------------------------------------------------------------------------
        // СОХРАНЕНИЕ / ЗАГРУЗКА СОСТОЯНИЯ ПРОХОЖДЕНИЯ (SaveData)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Возвращает текущее состояние активных арок для сериализации.
        /// </summary>
        public List<ArcSaveData> GetArcProgress()
        {
            var list = new List<ArcSaveData>();
            foreach (var arc in activeArcs)
            {
                if (arc == null || arc.definition == null) continue;
                var data = new ArcSaveData
                {
                    arcID = arc.definition.arcID,
                    startDay = arc.startDay,
                    currentStageIndex = arc.currentStageIndex,
                    isCompleted = arc.isCompleted,
                    stageActivated = arc.stageActivated != null ? (bool[])arc.stageActivated.Clone() : new bool[0]
                };
                list.Add(data);
            }
            return list;
        }

        /// <summary>
        /// Восстанавливает состояние активных арок из сохранения.
        /// </summary>
        public void LoadArcProgress(List<ArcSaveData> saveDataList)
        {
            if (saveDataList == null) return;

            activeArcs.Clear();
            plannedEncounters.Clear();

            foreach (var data in saveDataList)
            {
                if (data == null) continue;
                var def = allArcDefinitions.Find(a => a != null && a.arcID == data.arcID);
                if (def == null) continue;

                var arc = new ArcInstance
                {
                    definition = def,
                    startDay = data.startDay,
                    currentStageIndex = data.currentStageIndex,
                    isCompleted = data.isCompleted,
                    stageActivated = data.stageActivated != null ? (bool[])data.stageActivated.Clone() : new bool[def.stages.Count]
                };
                activeArcs.Add(arc);
            }

            _isInitialized = true;
            RefreshDebugInfo();
            Debug.Log($"[ArcManager] Восстановлено {activeArcs.Count} активных арок из сохранения.");
        }

        // -------------------------------------------------------------------------
        // API ДЛЯ ОТЛАДКИ
        // -------------------------------------------------------------------------

        private void RefreshDebugInfo()
        {
            debugActiveArcs.Clear();
            foreach (var arc in activeArcs)
            {
                if (arc?.definition == null) continue;
                debugActiveArcs.Add($"{arc.definition.arcID} [стадия {arc.currentStageIndex}/{arc.StageCount}] {(arc.isCompleted ? "✓" : "")}");
            }
            debugQueuedEncounters = plannedEncounters.Count;
        }

        public int ActiveArcCount => activeArcs.Count;
        public int QueuedEncounterCount => plannedEncounters.Count;
        public List<ArcInstance> GetActiveArcsSnapshot() => new List<ArcInstance>(activeArcs);
        public List<ArcDefinition> GetAllArcDefinitions() => new List<ArcDefinition>(allArcDefinitions);

        /// <summary>
        /// Принудительно завершает арку (для отладки).
        /// </summary>
        public void DebugCompleteArc(ArcInstance arc)
        {
            if (arc == null) return;
            while (arc.HasMoreStages)
            {
                arc.currentStageIndex++;
            }
            CompleteArc(arc);
            RefreshDebugInfo();
        }

        /// <summary>
        /// Переопределяет фон диалога для конкретного этапа конкретной арки во время выполнения
        /// (для динамической системы задников). Изменения видны на следующем запуске диалога
        /// (StartNode.defaultBackground читается один раз в DialogueUIManager.StartDialogue).
        /// </summary>
        /// <param name="arcID">ID арки, или null — тогда применить ко всем.</param>
        /// <param name="stageIndex">Индекс этапа, или -1 — тогда ко всем этапам указанной арки.</param>
        /// <param name="newBackground">Новый фон, или null — сбросить на null (DialogueUIManager.defaultBackground останется).</param>
        public void OverrideStageBackground(string arcID, int stageIndex, Sprite newBackground)
        {
            if (activeArcs == null) return;

            foreach (var arcInst in activeArcs)
            {
                if (arcInst == null || arcInst.definition == null) continue;
                if (!string.IsNullOrEmpty(arcID) && arcInst.definition.arcID != arcID) continue;

                for (int i = 0; i < arcInst.definition.stages.Count; i++)
                {
                    if (stageIndex >= 0 && i != stageIndex) continue;
                    var stage = arcInst.definition.stages[i];
                    if (stage != null && stage.dialogue != null && stage.dialogue.startNode is StartNode sn)
                    {
                        sn.defaultBackground = newBackground;
#if UNITY_EDITOR
                        UnityEditor.EditorUtility.SetDirty(sn);
#endif
                    }
                }
            }
        }
    }
}