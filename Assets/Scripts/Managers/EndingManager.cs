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
    /// Менеджер концовок. Подписан на TimeManager.OnDayChanged.
    /// На finalDay запускает визит Инспектора.
    /// При превышении лимита страйков — досрочное отстранение.
    /// После диалога Инспектора (с EventNode AddTraitPoint при ничьей) — вычисляет доминанту и запускает TriggerEnding.
    /// </summary>
    public class EndingManager : MonoBehaviour
    {
        public static EndingManager Instance { get; private set; }

        [Header("База концовок")]
        [SerializeField] private EndingDatabase endingDatabase;

        [Header("Граф визита Инспектора (день 30)")]
        [Tooltip("CinematicGraph, проигрываемый при наступлении finalDay. Если null — берётся по имени 'InspectorVisit' из Resources/CinematicGraphs/.")]
        [SerializeField] private CinematicGraph inspectorVisitCinematic;

        [Header("Диалог Инспектора (опционально)")]
        [SerializeField] private DialogueSystem.Data.DialogueGraph inspectorDialogue;

        [Header("Ключ графа визита в Resources/CinematicGraphs/")]
        [SerializeField] private string inspectorVisitGraphName = "InspectorVisit";

        public bool isEndingTriggered { get; private set; }
        public string selectedEndingID { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(this);
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayChanged -= OnDayChanged;
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnDayChanged += OnDayChanged;
            else
                Debug.LogWarning("[EndingManager] TimeManager.Instance не найден в Start(). OnDayChanged не будет вызван.");
        }

        private void OnDayChanged(int day)
        {
            if (isEndingTriggered) return;

            CheckForEarlyEnding();

            int finalDay = AIBalanceConfig.Instance != null ? AIBalanceConfig.Instance.finalDay : 30;
            if (day >= finalDay)
            {
                CheckForDay30Ending();
            }
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

        public void CheckForDay30Ending()
        {
            if (isEndingTriggered) return;

            CinematicGraph graph = inspectorVisitCinematic;
            if (graph == null)
            {
                graph = CinematicGraphLibrary.LoadGraph(inspectorVisitGraphName);
            }

            if (graph == null)
            {
                Debug.LogError($"[EndingManager] Не удалось загрузить InspectorVisit (имя: {inspectorVisitGraphName}).");
                DetermineAndTrigger(null);
                return;
            }

            var player = FindFirstObjectByType<CinematicPlayer>();
            if (player == null)
            {
                Debug.LogError("[EndingManager] CinematicPlayer не найден на сцене.");
                DetermineAndTrigger(null);
                return;
            }

            player.OnFinished -= OnInspectorCinematicFinished;
            player.OnFinished += OnInspectorCinematicFinished;

            Debug.Log("[EndingManager] Запуск визита Инспектора (день 30).");
            player.Play(graph);
        }

        private void OnInspectorCinematicFinished()
        {
            var player = FindFirstObjectByType<CinematicPlayer>();
            if (player != null) player.OnFinished -= OnInspectorCinematicFinished;

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
                        if (SaveLoadManager.Instance != null)
                            SaveLoadManager.Instance.LoadGame(SaveLoadManager.Instance.GetCurrentSlot());
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
            // Сохраняем изменения обратно через JsonUtility (перезапись того же файла)
            string path = System.IO.Path.Combine(Application.persistentDataPath, $"save_slot_{slot}.json");
            string json = JsonUtility.ToJson(data, true);
            System.IO.File.WriteAllText(path, json);
            Debug.Log($"[EndingManager] gameCompleted=true, день={data.day}, слот={slot}");
        }
    }
}
