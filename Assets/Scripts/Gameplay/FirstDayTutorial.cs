// Assets/Scripts/Gameplay/FirstDayTutorial.cs
using System.Collections;
using UnityEngine;
using Managers;
using BuroDebug;
using DialogueSystem.Data;
using Utilities;

namespace Gameplay
{
    public class FirstDayTutorial : MonoBehaviour
    {
        [Header("Точки маршрута")]
        public Transform entryPoint;
        public Waypoint firstStopPoint;
        public Waypoint secondStopPoint;
        public Transform finalDeskPoint;

        [Header("Диалоги")]
        public DialogueGraph firstDialogue;
        public DialogueGraph secondDialogue;

        [Header("UI подсказка")]
        public GameObject hintArrow;

        [Header("Звук")]
        public Scriptables.Audio.SoundID secretaryRingSound = Scriptables.Audio.SoundID.Voice_Secretary;

        [Header("Ссылки на компоненты")]
        public DirectorAvatarController directorAvatar;
        public CameraToggle cameraToggle;
        public DirectorDebugCamera debugCamera;
        public PlayerInputController playerInput;
        
        /// <summary>
        /// Флаг, что туториал завершён
        /// </summary>
        public bool IsTutorialCompleted { get; private set; } = false;
        
        /// <summary>
        /// Флаг, что туториал ждёт клика по кнопке стола директора
        /// </summary>
        public bool IsWaitingForDeskClick { get; private set; } = false;

        private void Awake()
        {
            // Автоматически находим компоненты, если не назначены в инспекторе
            if (directorAvatar == null)
                directorAvatar = FindFirstObjectByType<DirectorAvatarController>();
            
            if (cameraToggle == null)
                cameraToggle = FindFirstObjectByType<CameraToggle>();
            
            if (debugCamera == null)
                debugCamera = FindFirstObjectByType<DirectorDebugCamera>();
            
            if (playerInput == null)
                playerInput = FindFirstObjectByType<PlayerInputController>();
            
        }

        public void StartTutorial()
        {
            Debug.Log("[FirstDayTutorial] StartTutorial: Начало туториала первого дня");
            
            // Гарантируем, что время идёт (на случай, если пауза осталась от сплеш-скрина)
            if (Time.timeScale == 0f) Time.timeScale = 1f;
            
            // Находим все компоненты
            if (directorAvatar == null) directorAvatar = FindFirstObjectByType<DirectorAvatarController>();
            if (cameraToggle == null) cameraToggle = FindFirstObjectByType<CameraToggle>();
            if (debugCamera == null) debugCamera = FindFirstObjectByType<DirectorDebugCamera>();
            if (playerInput == null) playerInput = FindFirstObjectByType<PlayerInputController>();

            // Отключаем CameraToggle и включаем DirectorDebugCamera в режим слежения
            if (cameraToggle != null)
                cameraToggle.enabled = false;
            
            if (debugCamera != null)
            {
                debugCamera.enabled = true;
                debugCamera.SetFollowMode(true);
            }

            // Блокируем игровой ввод (курсор переключится автоматически через свойство IsCutscenePlaying)
            if (playerInput != null)
                playerInput.IsCutscenePlaying = true;

            // Телепортируем директора в точку входа
            if (directorAvatar != null && entryPoint != null)
            {
                directorAvatar.TeleportTo(entryPoint.position);
            }

            // Запускаем корутину движения и диалогов
            if (directorAvatar != null)
                StartCoroutine(MoveAndTalk());
        }

        private IEnumerator MoveAndTalk()
        {
            Debug.Log("[FirstDayTutorial] MoveAndTalk: Начало движения и диалогов");
            
            // 1. Движение к первой точке
            yield return MoveDirectorToWaypoint(firstStopPoint);
            yield return PlayDialogueAndWait(firstDialogue);
            
            // 2. Движение ко второй точке
            yield return MoveDirectorToWaypoint(secondStopPoint);
            yield return PlayDialogueAndWait(secondDialogue);
            
            // 3. Передача управления игроку
            EndCutsceneAndGiveControl();
        }

        private IEnumerator MoveDirectorToWaypoint(Waypoint target)
        {
            if (directorAvatar == null || target == null)
            {
                Debug.LogWarning("[FirstDayTutorial] MoveDirectorToWaypoint: directorAvatar или target == null");
                yield break;
            }

            var agentMover = directorAvatar.AgentMover;
            if (agentMover == null)
            {
                Debug.LogWarning("[FirstDayTutorial] MoveDirectorToWaypoint: AgentMover == null");
                yield break;
            }

            // Блокируем управление игрока на время движения
            if (playerInput != null)
            {
                playerInput.IsCutscenePlaying = true;
            }

            // Строим путь
            var path = PathfindingUtility.BuildPathTo(directorAvatar.transform.position, target.transform.position, directorAvatar.gameObject);
            if (path == null || path.Count == 0)
            {
                Debug.LogWarning("[FirstDayTutorial] MoveDirectorToWaypoint: путь не найден");
                if (playerInput != null) playerInput.IsCutscenePlaying = false;
                yield break;
            }

            // Оптимизация: проверяем расстояние до первого вейпоинта
            if (path.Count > 0)
            {
                var firstWP = path.Peek();
                float distToFirst = Vector2.Distance(directorAvatar.transform.position, firstWP.transform.position);
                if (distToFirst < agentMover.stoppingDistance)
                {
                    path.Dequeue(); // Удаляем первый вейпоинт из очереди
                }
            }

            // Устанавливаем путь
            agentMover.SetPath(path);

            // Ждём окончания движения
            yield return new WaitUntil(() => !agentMover.IsMoving());

            // Разблокируем управление
            if (playerInput != null)
            {
                playerInput.IsCutscenePlaying = false;
            }

            Debug.Log($"[FirstDayTutorial] MoveDirectorToWaypoint: достигнута точка {target.name}");
        }

        private IEnumerator PlayDialogueAndWait(DialogueGraph dialogue)
        {
            if (dialogue == null)
            {
                Debug.LogWarning("[FirstDayTutorial] PlayDialogueAndWait: dialogue == null");
                yield break;
            }

            if (DialogueUIManager.Instance == null)
            {
                Debug.LogWarning("[FirstDayTutorial] PlayDialogueAndWait: DialogueUIManager.Instance == null");
                yield break;
            }

            Debug.Log($"[FirstDayTutorial] PlayDialogueAndWait: запуск диалога {dialogue.name}");

            // Флаг завершения диалога
            bool dialogueEnded = false;
            
            // Callback при завершении диалога (через onComplete параметр StartDialogue)
            System.Action onDialogueComplete = () =>
            {
                Debug.Log($"[FirstDayTutorial] PlayDialogueAndWait: диалог {dialogue.name} завершён");
                dialogueEnded = true;
            };

            // Запускаем диалог с callback'ом
            DialogueUIManager.Instance.StartDialogue(dialogue, null, onDialogueComplete);

            // Ждём завершения диалога
            yield return new WaitUntil(() => dialogueEnded);
            
            // Время уже восстановлено DialogueUIManager после диалога
        }

        private void EndCutsceneAndGiveControl()
        {
            Debug.Log("[FirstDayTutorial] EndCutsceneAndGiveControl: Передача управления игроку");

            // Разблокируем игровой ввод (курсор восстановится автоматически через свойство)
            if (playerInput != null)
                playerInput.IsCutscenePlaying = false;

            // НЕ отключаем DirectorDebugCamera - оставляем активной для игрока

            // Воспроизводим звук звонка секретаря
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(secretaryRingSound);
            }

            // Активируем UI-стрелочку подсказки и ждём клика по кнопке стола
            if (hintArrow != null)
            {
                hintArrow.SetActive(true);
            }
            
            // Ждём клика по кнопке стола
            IsWaitingForDeskClick = true;
        }

        /// <summary>
        /// Вызывается из ShowDirectorDeskButton, когда игрок кликает кнопку стола во время туториала
        /// </summary>
        public void OnDeskButtonClickedDuringTutorial()
        {
            if (!IsWaitingForDeskClick)
            {
                Debug.LogWarning("[FirstDayTutorial] OnDeskButtonClickedDuringTutorial: клик получен, но IsWaitingForDeskClick == false");
                return;
            }
            
            Debug.Log("[FirstDayTutorial] OnDeskButtonClickedDuringTutorial: Игрок кликнул кнопку стола!");
            
            IsWaitingForDeskClick = false;
            
            // Скрываем стрелочку
            if (hintArrow != null)
            {
                hintArrow.SetActive(false);
            }
            
            // Блокируем управление
            if (playerInput != null)
                playerInput.IsCutscenePlaying = true;
            
            // Запускаем движение к столу директора
            if (finalDeskPoint != null)
            {
                StartCoroutine(MoveDirectorToDeskAndCompleteTutorial());
            }
            else
            {
                CompleteTutorialDirectly();
            }
        }
        
        private IEnumerator MoveDirectorToDeskAndCompleteTutorial()
        {
            Debug.Log("[FirstDayTutorial] MoveDirectorToDeskAndCompleteTutorial: Движение к столу директора");
            
            // Если finalDeskPoint - это Waypoint, используем MoveDirectorToWaypoint
            if (finalDeskPoint.GetComponent<Waypoint>() != null)
            {
                yield return MoveDirectorToWaypoint(finalDeskPoint.GetComponent<Waypoint>());
            }
            else
            {
                // Иначе телепортируемся напрямую
                if (directorAvatar != null)
                {
                    directorAvatar.TeleportTo(finalDeskPoint.position);
                }
                yield return null;
            }
            
            // По прибытии
            CompleteTutorialDirectly();
        }
        
        private void OpenDirectorDeskPanel()
        {
            Debug.Log("[FirstDayTutorial] OpenDirectorDeskPanel: Открытие панели стола директора");
            
            var startOfDayPanel = FindObjectOfType<StartOfDayPanel>(true);
            if (startOfDayPanel == null)
            {
                Debug.LogError("[FirstDayTutorial] OpenDirectorDeskPanel: StartOfDayPanel не найден!");
                return;
            }

            // Активируем панель и запускаем анимацию
            startOfDayPanel.gameObject.SetActive(true);
            var animator = startOfDayPanel.GetComponent<UIWindowAnimator>();
            if (animator != null)
                animator.Open();
            else
            {
                var cg = startOfDayPanel.GetComponent<CanvasGroup>();
                if (cg != null) { cg.alpha = 1f; cg.interactable = true; cg.blocksRaycasts = true; }
            }

            // Включаем музыку стола
            if (MusicPlayer.Instance != null)
                MusicPlayer.Instance.PlayDirectorsOfficeTheme();
        }
        
        private void CompleteTutorialDirectly()
        {
            Debug.Log("[FirstDayTutorial] CompleteTutorialDirectly: Завершение туториала");
            
            // Устанавливаем состояние "за столом"
            if (directorAvatar != null)
            {
                directorAvatar.ForceSetAtDeskState(true);
            }
            
            // Выключаем DirectorDebugCamera, включаем CameraToggle
            if (debugCamera != null)
                debugCamera.enabled = false;
            if (cameraToggle != null)
                cameraToggle.enabled = true;
            
            // Открываем панель стола директора
            OpenDirectorDeskPanel();
            
            // Снимаем блокировку управления (ПОСЛЕ открытия панели)
            if (playerInput != null)
                playerInput.IsCutscenePlaying = false;
            
            // Сохраняем в SaveData, что туториал первого дня завершён
            int currentSlot = SaveLoadManager.Instance.GetCurrentSlot();
            SaveData data = SaveLoadManager.Instance.GetDataForSlot(currentSlot);
            if (data != null)
            {
                data.firstDayTutorialCompleted = true;
                SaveLoadManager.Instance.SaveGame(currentSlot);
                Debug.Log("[FirstDayTutorial] firstDayTutorialCompleted = true сохранено");
            }
            
            // Уничтожаем компонент туториала
            Destroy(gameObject);
        }

        private void CleanupTutorial()
        {
            Debug.Log("[FirstDayTutorial] CleanupTutorial: Завершение туториала");
            
            // Возвращаем управление камерой
            if (cameraToggle != null)
                cameraToggle.enabled = true;
            
            // НЕ отключаем DirectorDebugCamera - оставляем игроку управление камерой
            
            // Разблокируем игровой ввод (курсор восстановится автоматически через свойство)
            if (playerInput != null)
                playerInput.IsCutscenePlaying = false;
        }
    }
}
