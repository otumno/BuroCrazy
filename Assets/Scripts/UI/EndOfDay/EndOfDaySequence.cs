using System.Collections;
using CinematicSystem;
using Managers;
using UI.Bookkeeping;
using UnityEngine;

namespace UI.EndOfDay
{
    /// <summary>
    /// Цепочка экранов между первой и второй половиной ночи: итоги дня → планирование смен → приказ на завтра.
    /// Держит смену дня в TimeManager, пока игрок не пройдёт все экраны, затем сажает директора за стол.
    /// </summary>
    public class EndOfDaySequence : MonoBehaviour
    {
        [SerializeField] private BookkeepingPanelUI bookkeepingPanel;
        [SerializeField] private StaffSchedulePanelUI schedulePanel;
        [SerializeField] private OrderSelectionUI orderSelection;

        private TimeManager _timeManager;
        private CinematicPlayer _cinematicPlayer;
        private StartOfDayPanel _desk;

        private void Start()
        {
            _timeManager = TimeManager.Instance;
            if (_timeManager == null)
            {
                Debug.LogError("[EndOfDaySequence] TimeManager не найден, экраны конца дня не будут показаны.");
                return;
            }

            _cinematicPlayer = FindFirstObjectByType<CinematicPlayer>(FindObjectsInactive.Include);
            _desk = FindFirstObjectByType<StartOfDayPanel>(FindObjectsInactive.Include);
            _timeManager.DayEnding += TimeManager_DayEnding;
        }

        private void OnDestroy()
        {
            if (_timeManager != null) _timeManager.DayEnding -= TimeManager_DayEnding;
        }

        private void TimeManager_DayEnding()
        {
            if (IsEndingTriggered()) return;

            _timeManager.HoldDayEnd();
            StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            yield return new WaitWhile(IsCutsceneOrDialogueActive);

            MainUIManager.Instance?.PushPause();
            // Окно планирования открыто до «Начать день»: смены можно поправить и со стола после цепочки.
            ShiftPlanning.Open();

            yield return ShowDayResults();
            yield return ShowSchedulePlanning();
            yield return ShowOrderSelection();

            // Смена дня: OnDayChanged запускает катсцены нового дня, страйки и проверку концовок.
            _timeManager.ReleaseDayEnd();

            bool isPausedBySequence = true;
            if (IsCutsceneOrDialogueActive())
            {
                // Катсцены рассчитаны на идущее время (движение персонажей, ожидания), поэтому на время катсцены паузу отпускаем.
                MainUIManager.Instance?.PopPause();
                isPausedBySequence = false;
                yield return new WaitWhile(IsCutsceneOrDialogueActive);
            }

            // Концовка показывает свой экран, а катсцена могла сама открыть стол (как туториал) — тогда стол не нужен.
            if (!IsEndingTriggered() && !IsDeskOpen())
            {
                ReturnDirectorToDesk();
                // Стол ставит свою паузу до «Начать день»; нашу снимаем после, чтобы между ними не было кадра с идущим временем.
                MainUIManager.Instance?.ShowDirectorDesk();
            }

            if (isPausedBySequence) MainUIManager.Instance?.PopPause();
        }

        private IEnumerator ShowDayResults()
        {
            if (bookkeepingPanel == null) yield break;

            bool isClosed = false;
            void BookkeepingPanel_Closed() => isClosed = true;

            bookkeepingPanel.Closed += BookkeepingPanel_Closed;
            bookkeepingPanel.ShowDayResults();
            yield return new WaitUntil(() => isClosed);
            bookkeepingPanel.Closed -= BookkeepingPanel_Closed;
        }

        private IEnumerator ShowSchedulePlanning()
        {
            if (schedulePanel == null) yield break;

            bool isClosed = false;
            void SchedulePanel_Closed() => isClosed = true;

            schedulePanel.Closed += SchedulePanel_Closed;
            schedulePanel.ShowPlanning();
            yield return new WaitUntil(() => isClosed);
            schedulePanel.Closed -= SchedulePanel_Closed;
        }

        private IEnumerator ShowOrderSelection()
        {
            if (orderSelection == null) yield break;

            bool isSelected = false;
            void OrderSelection_OrderSelected() => isSelected = true;

            orderSelection.OrderSelected += OrderSelection_OrderSelected;
            orderSelection.Show();
            yield return new WaitUntil(() => isSelected);
            orderSelection.OrderSelected -= OrderSelection_OrderSelected;
        }

        private static void ReturnDirectorToDesk()
        {
            var director = DirectorAvatarController.Instance;
            if (director == null || director.directorChairPoint == null) return;

            director.StopManualWork(false);
            director.TeleportTo(director.directorChairPoint.position);
            director.ForceSetAtDeskState(true);
        }

        private bool IsCutsceneOrDialogueActive()
        {
            bool isCutscenePlaying = _cinematicPlayer != null && _cinematicPlayer.IsPlaying;
            bool isDialogueActive = DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsDialogueActive;
            return isCutscenePlaying || isDialogueActive;
        }

        // Стол прячется через alpha, а не SetActive, поэтому одного activeInHierarchy недостаточно.
        private bool IsDeskOpen()
        {
            if (_desk == null || !_desk.gameObject.activeInHierarchy) return false;

            var canvasGroup = _desk.GetComponent<CanvasGroup>();
            return canvasGroup == null || canvasGroup.alpha > 0.01f;
        }

        private static bool IsEndingTriggered() => EndingManager.Instance != null && EndingManager.Instance.isEndingTriggered;
    }
}
