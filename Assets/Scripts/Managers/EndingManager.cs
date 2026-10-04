using System.Collections.Generic;
using CinematicSystem;
using Data;
using DialogueSystem.Data;
using Gameplay;
using UI;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// Менеджер концовок. Подписан на TimeManager.OnDayChanged (досрочное отстранение по страйкам).
    /// Визит Инспектора — обычное событие CinematicTriggerManager (триггер InspectorVisit, начало finalDay):
    /// граф запускает менеджер событий, а EndingManager только реагирует на его окончание.
    /// После диалога Инспектора (с EventNode AddTraitPoint при ничьей) — вычисляет доминанту и запускает TriggerEnding.
    /// </summary>
    public class EndingManager : MonoBehaviour
    {
        public const string InspectorVisitTriggerId = "InspectorVisit";

        public static EndingManager Instance { get; private set; }

        [Header("База концовок")]
        [SerializeField] private EndingDatabase endingDatabase;

        [Header("Диалог Инспектора (опционально)")]
        [SerializeField] private DialogueSystem.Data.DialogueGraph inspectorDialogue;

        public bool isEndingTriggered { get; private set; }
        public string selectedEndingID { get; private set; }

        private int FinalDay => AIBalanceConfig.Instance != null ? AIBalanceConfig.Instance.finalDay : 30;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(this);
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayChanged -= OnDayChanged;
            if (CinematicTriggerManager.Instance != null)
                CinematicTriggerManager.Instance.OnTriggerCompleted -= CinematicTriggerManager_TriggerCompleted;
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayChanged += OnDayChanged;
            else
                Debug.LogWarning("[EndingManager] TimeManager.Instance не найден в Start(). OnDayChanged не будет вызван.");

            var triggerManager = CinematicTriggerManager.Instance;
            if (triggerManager == null)
            {
                Debug.LogWarning("[EndingManager] CinematicTriggerManager не найден — визит Инспектора не запустится.");
                return;
            }

            triggerManager.OnTriggerCompleted += CinematicTriggerManager_TriggerCompleted;

            // День визита задаёт баланс (AIBalanceConfig.finalDay), а не значение в списке триггеров сцены —
            // чтобы финальный день настраивался в одном месте.
            var inspectorVisit = triggerManager.GetTrigger(InspectorVisitTriggerId);
            if (inspectorVisit != null) inspectorVisit.requiredDay = FinalDay;
            else Debug.LogWarning($"[EndingManager] В CinematicTriggerManager нет триггера '{InspectorVisitTriggerId}'.");
        }

        private void OnDayChanged(int day)
        {
            if (isEndingTriggered) return;

            CheckForEarlyEnding();

            // Если события визита нет (или у него нет графа), концовку всё равно нужно дать.
            if (day >= FinalDay && !HasInspectorVisitCinematic())
            {
                Debug.LogError($"[EndingManager] Нет катсцены '{InspectorVisitTriggerId}' — определяем концовку без визита.");
                DetermineAndTrigger(null);
            }
        }

        private static bool HasInspectorVisitCinematic()
        {
            var trigger = CinematicTriggerManager.Instance != null ? CinematicTriggerManager.Instance.GetTrigger(InspectorVisitTriggerId) : null;
            return trigger != null && trigger.enabled && trigger.graphToPlay != null;
        }

        private void CinematicTriggerManager_TriggerCompleted(string triggerId)
        {
            if (triggerId == InspectorVisitTriggerId) OnInspectorCinematicFinished();
        }

        public void CheckForEarlyEnding()
        {
            if (isEndingTriggered) return;
            if (DirectorManager.Instance == null) return;

            int limit = AIBalanceConfig.Instance != null ? AIBalanceConfig.Instance.dismissalStrikeLimit : 5;
            if (DirectorManager.Instance.currentStrikes >= limit)
            {
                Debug.Log($"[EndingManager] Лимит страйков ({limit}) превышен → отстранение.");
                TriggerEnding(TraitManager.TRAIT_DISMISSAL);
            }
        }

        private void OnInspectorCinematicFinished()
        {
            if (isEndingTriggered) return;

            var dominants = TraitManager.Instance != null ? TraitManager.Instance.GetDominantTraits() : new List<string>();
            if (dominants.Count > 1)
            {
                Debug.Log($"[EndingManager] Ничья между {dominants.Count} чертами: [{string.Join(", ", dominants)}]. Ждём ответ Инспектора через EventNode.AddTraitPoint в диалоге.");
                return;
            }

            DetermineAndTrigger(null);
        }

        /// <summary>
        /// Вызывается после диалога Инспектора, когда игрок сделал выбор при ничьей.
        /// TraitManager.AddTraitPoints уже добавлен внутри диалога (EventNode).
        /// </summary>
        public void OnInspectorQuestionAnswered()
        {
            if (isEndingTriggered) return;
            DetermineAndTrigger(null);
        }

        public void DetermineAndTrigger(string _)
        {
            if (isEndingTriggered) return;

            string dominant = TraitManager.Instance != null ? TraitManager.Instance.GetDominantTrait() : null;
            if (string.IsNullOrEmpty(dominant))
            {
                Debug.LogWarning("[EndingManager] Не удалось определить доминирующую черту — все нули. Берём первую по умолчанию.");
                dominant = TraitManager.TRAIT_LAW;
            }

            selectedEndingID = dominant;
            TriggerEnding(dominant);
        }

        public void TriggerEnding(string endingID)
        {
            if (isEndingTriggered) return;
            if (string.IsNullOrEmpty(endingID)) return;

            isEndingTriggered = true;
            selectedEndingID = endingID;

            Debug.Log($"[EndingManager] Запуск концовки: {endingID}");

            if (MainUIManager.Instance != null) MainUIManager.Instance.PushPause();

            if (endingDatabase == null)
            {
                Debug.LogError("[EndingManager] endingDatabase не назначен! Невозможно показать концовку.");
                return;
            }

            var entry = endingDatabase.Find(endingID);
            if (entry == null)
            {
                Debug.LogError($"[EndingManager] Концовка '{endingID}' не найдена в EndingDatabase.");
                return;
            }

            if (entry.isDismissal)
            {
                AudioClip music = AIBalanceConfig.Instance != null && AIBalanceConfig.Instance.dismissalMusicOverride != null
                    ? AIBalanceConfig.Instance.dismissalMusicOverride
                    : entry.endingMusic;
                if (music != null && MusicPlayer.Instance != null) MusicPlayer.Instance.PlayOneShotTrack(music);

                DismissalScreenUI.Instance?.ShowDismissal(
                    onReload: () =>
                    {
                        if (MainUIManager.Instance != null) MainUIManager.Instance.PopPause();
                        GameSession.ReloadCurrentSlot();
                    },
                    onMenu: () =>
                    {
                        if (MainUIManager.Instance != null) MainUIManager.Instance.PopPause();
                        if (MainUIManager.Instance != null) MainUIManager.Instance.GoToMainMenu();
                    });
            }
            else
            {
                if (entry.endingMusic != null && MusicPlayer.Instance != null)
                    MusicPlayer.Instance.PlayOneShotTrack(entry.endingMusic);

                EndingBookUI.Instance?.ShowEnding(entry,
                    onExit: () =>
                    {
                        if (SaveLoadManager.Instance != null)
                        {
                            // Выставляем флаг завершения игры ДО сохранения
                            MarkGameCompletedInCurrentSave();
                            SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.GetCurrentSlot());
                        }
                        if (MainUIManager.Instance != null) MainUIManager.Instance.PopPause();
                        if (MainUIManager.Instance != null) MainUIManager.Instance.GoToMainMenu();
                    });
            }
        }

        public void TriggerDismissalEarly()
        {
            TriggerEnding(TraitManager.TRAIT_DISMISSAL);
        }

        public EndingDatabase GetDatabase() => endingDatabase;

        /// <summary>
        /// Помечает текущее сохранение как завершённое (gameCompleted=true).
        /// Вызывается непосредственно перед SaveGame после показа книги учёта.
        /// При отстранении флаг НЕ выставляется.
        /// </summary>
        private void MarkGameCompletedInCurrentSave()
        {
            if (SaveLoadManager.Instance == null) return;
            int slot = SaveLoadManager.Instance.GetCurrentSlot();
            var data = SaveLoadManager.Instance.GetDataForSlot(slot);
            if (data == null) return;
            data.gameCompleted = true;
            // day = finalDay — финальное сохранение всегда на день finalDay
            int finalDay = AIBalanceConfig.Instance != null ? AIBalanceConfig.Instance.finalDay : 30;
            if (data.day < finalDay) data.day = finalDay;
            // Следом вызывается SaveGame, а он пишет день из CalendarManager — подтягиваем и его, иначе день откатится.
            if (CalendarManager.Instance != null && CalendarManager.Instance.CurrentDay < data.day)
                CalendarManager.Instance.SetDay(data.day);
            // Сохраняем изменения обратно через JsonUtility (перезапись того же файла)
            string path = System.IO.Path.Combine(Application.persistentDataPath, $"save_slot_{slot}.json");
            string json = JsonUtility.ToJson(data, true);
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"[EndingManager] gameCompleted=true, день={data.day}, слот={slot}");
        }
    }
}
