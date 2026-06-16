// === FILE: Assets/Scripts/Cinematic/CinematicPlayer.cs ===
using System;
using System.Collections;
using UnityEngine;
using Managers;

namespace CinematicSystem
{
    /// <summary>
    /// Режим выполнения кинематического графа.
    /// </summary>
    public enum ExecutionMode
    {
        /// <summary>Полный контроль - блокирует управление, курсор, управляет камерой</summary>
        FullControl,
        
        /// <summary>Фоновый режим - не блокирует управление, не меняет курсор</summary>
        Background
    }

    /// <summary>
    /// Компонент для выполнения кинематического графа.
    /// Последовательно выполняет узлы, начиная со startNode.
    /// </summary>
    public class CinematicPlayer : MonoBehaviour
    {
        /// <summary>Текущий выполняемый граф</summary>
        public CinematicGraph CurrentGraph { get; private set; }
        
        /// <summary>Идёт ли воспроизведение</summary>
        public bool IsPlaying { get; private set; }
        
        /// <summary>Поставлен ли на паузу</summary>
        public bool IsPaused { get; private set; }
        
        /// <summary>Можно ли пропускать узлы</summary>
        public bool CanSkip { get; set; } = true;
        
        /// <summary>
        /// Актор по умолчанию для графов без указания characterID.
        /// Устанавливается из StartNode в начале выполнения графа.
        /// </summary>
        public string DefaultActor { get; set; } = "Director";
        
        /// <summary>Текущий режим выполнения</summary>
        private ExecutionMode currentMode = ExecutionMode.FullControl;
        
        /// <summary>Заблокировано ли управление (для отслеживания состояния)</summary>
        private bool inputLocked = false;

        private Coroutine executionCoroutine;
        private CinematicNode currentNode;
        private bool skipRequested;

        /// <summary>Событие завершения воспроизведения графа</summary>
        public event Action OnFinished;
        
        /// <summary>Событие начала выполнения узла</summary>
        public event Action<CinematicNode> OnNodeStarted;
        
        /// <summary>Событие завершения узла</summary>
        public event Action<CinematicNode> OnNodeFinished;

        private void Awake()
        {
            // Кэшируем компоненты при создании
        }

        /// <summary>
        /// Начать выполнение графа с режимом FullControl.
        /// </summary>
        public void Play(CinematicGraph graph)
        {
            Play(graph, ExecutionMode.FullControl);
        }

        /// <summary>
        /// Начать выполнение графа с указанным режимом.
        /// </summary>
        /// <param name="graph">Граф для выполнения</param>
        /// <param name="executionMode">Режим выполнения</param>
        public void Play(CinematicGraph graph, ExecutionMode executionMode)
        {
            Debug.Log($"[CinematicPlayer] Play() called. Graph={graph?.name ?? "NULL"}, allNodes.Count={graph?.allNodes?.Count ?? 0}, startNode={graph?.startNode?.nodeName ?? "NULL"}");
            
            if (IsPlaying) Stop();
            
            CurrentGraph = graph;
            currentMode = executionMode;
            IsPlaying = true;
            IsPaused = false;
            skipRequested = false;
            
            // Восстанавливаем связи из сериализованного списка (гарантия целостности)
            if (graph != null)
            {
                graph.RestoreLinks();
            }
            
            // Блокируем управление только в режиме FullControl
            if (executionMode == ExecutionMode.FullControl)
            {
                var inputController = FindObjectOfType<PlayerInputController>();
                if (inputController != null)
                    inputController.IsCutscenePlaying = true;
                
                // Устанавливаем катсценный курсор
                var cursor = FindObjectOfType<CursorController>();
                if (cursor != null) cursor.SetCutsceneCursor(true);
                
                inputLocked = true;
            }
            else
            {
                inputLocked = false;
            }
            
            executionCoroutine = StartCoroutine(ExecuteGraph());
        }

        /// <summary>
        /// Остановить выполнение графа и сбросить состояние.
        /// </summary>
        public void Stop()
        {
            if (executionCoroutine != null)
            {
                StopCoroutine(executionCoroutine);
                executionCoroutine = null;
            }
            
            IsPlaying = false;
            IsPaused = false;
            currentNode = null;
            
            // Разблокируем управление только если оно было заблокировано
            if (inputLocked)
            {
                var inputController = FindObjectOfType<PlayerInputController>();
                if (inputController != null)
                    inputController.IsCutscenePlaying = false;
                
                // Возвращаем обычный курсор
                var cursor = FindObjectOfType<CursorController>();
                if (cursor != null) cursor.SetCutsceneCursor(false);
                
                inputLocked = false;
            }
        }

        /// <summary>
        /// Поставить выполнение на паузу.
        /// </summary>
        public void Pause()
        {
            if (!IsPlaying) return;
            IsPaused = true;
        }

        /// <summary>
        /// Возобновить выполнение после паузы.
        /// </summary>
        public void Resume()
        {
            if (!IsPlaying) return;
            IsPaused = false;
        }

        /// <summary>
        /// Пропустить текущий узел (если разрешено и выполняется).
        /// </summary>
        public void Skip()
        {
            if (CanSkip && IsPlaying && !IsPaused)
            {
                skipRequested = true;
            }
        }

        /// <summary>
        /// Корутина выполнения графа.
        /// </summary>
        private IEnumerator ExecuteGraph()
        {
            Debug.Log($"[CinematicPlayer] ExecuteGraph START. Graph={(CurrentGraph != null ? CurrentGraph.name : "NULL")}, startNode={(CurrentGraph?.startNode != null ? CurrentGraph.startNode.nodeName : "NULL")}");
            
            if (CurrentGraph == null || CurrentGraph.startNode == null)
            {
                Debug.LogError("[CinematicPlayer] Не указан startNode в графе!");
                Finish();
                yield break;
            }

            currentNode = CurrentGraph.startNode;
            Debug.Log($"[CinematicPlayer] Начальный currentNode: {currentNode.nodeName} (id={currentNode.id})");
            
            while (currentNode != null)
            {
                Debug.Log($"[CinematicPlayer] >>> Выполняю узел: {currentNode.nodeName} (type={currentNode.GetNodeType()})");
                OnNodeStarted?.Invoke(currentNode);
                skipRequested = false;

                // Запускаем выполнение узла
                var execution = currentNode.Execute(this);
                
                while (execution.MoveNext())
                {
                    // Проверяем паузу
                    while (IsPaused && IsPlaying)
                    {
                        yield return null;
                    }
                    
                    // Проверяем пропуск
                    if (skipRequested && CanSkip)
                    {
                        break;
                    }
                    
                    if (execution.Current != null)
                    {
                        yield return execution.Current;
                    }
                    else
                    {
                        yield return null;
                    }
                }

                OnNodeFinished?.Invoke(currentNode);
                Debug.Log($"[CinematicPlayer] Узел завершён: {currentNode?.nodeName ?? "null"}, currentNode после = {currentNode?.nodeName ?? "null"}");

                // Если currentNode стал null - завершаем
                if (currentNode == null) break;
            }

            Debug.Log("[CinematicPlayer] ExecuteGraph END - выход из while цикла");
            Finish();
        }

        /// <summary>
        /// Вызывается из узла для перехода к следующему.
        /// </summary>
        public void GoToNextNode(CinematicNode nextNode)
        {
            Debug.Log($"[CinematicPlayer] GoToNextNode: {currentNode?.nodeName ?? "null"} -> {nextNode?.nodeName ?? "NULL (завершение графа)"}");
            currentNode = nextNode;
        }
        
        /// <summary>
        /// Получить ID персонажа для узла. Если указан маркер "$Actor" или пустая строка,
        /// возвращается DefaultActor.
        /// </summary>
        /// <param name="characterID">ID персонажа из узла</param>
        /// <returns>Реальный ID персонажа для использования</returns>
        public string ResolveCharacterID(string characterID)
        {
            if (string.IsNullOrEmpty(characterID) || characterID == "$Actor")
            {
                return DefaultActor;
            }
            return characterID;
        }

        /// <summary>
        /// Завершить выполнение графа.
        /// </summary>
        private void Finish()
        {
            IsPlaying = false;
            IsPaused = false;
            currentNode = null;
            
            // Разблокируем управление
            var inputController = FindObjectOfType<PlayerInputController>();
            if (inputController != null)
                inputController.IsCutscenePlaying = false;
            
            // Возвращаем обычный курсор
            var cursor = FindObjectOfType<CursorController>();
            if (cursor != null) cursor.SetCutsceneCursor(false);
            
            // ВОССТАНАВЛИВАЕМ УПРАВЛЕНИЕ КАМЕРОЙ
            RestoreCameraControl();
            
            OnFinished?.Invoke();
        }
        
        /// <summary>
        /// Восстанавливает стандартное управление камерой после завершения cinematic.
        /// Включает CameraToggle и отключает режим слежения в DirectorDebugCamera.
        /// </summary>
        private void RestoreCameraControl()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[CinematicPlayer] Не найдена MainCamera для восстановления управления камерой");
                return;
            }
            
            // Получаем компоненты камеры
            var cameraToggle = mainCamera.GetComponent<Managers.CameraToggle>();
            var directorCam = mainCamera.GetComponent<BuroDebug.DirectorDebugCamera>();
            
            // Включаем CameraToggle (стандартное управление)
            if (cameraToggle != null)
            {
                cameraToggle.enabled = true;
                Debug.Log("[CinematicPlayer] CameraToggle.enabled = true (восстановлен стандартный режим камеры)");
            }
            
            // Отключаем режим слежения DirectorDebugCamera
            if (directorCam != null)
            {
                directorCam.SetFollowMode(false);
                Debug.Log("[CinematicPlayer] DirectorDebugCamera.SetFollowMode(false) (режим слежения отключён)");
            }
            
            // Дополнительная защита: только сбрасываем orthographicSize, НЕ отключаем DirectorDebugCamera.enabled
            // Компонент должен оставаться enabled чтобы игрок мог нажать C и переключить режим
            if (directorCam != null && mainCamera != null)
            {
                // Сбрасываем зум камеры на дефолтный (используем defaultSize из DirectorDebugCamera)
                mainCamera.orthographicSize = directorCam.defaultSize;
                Debug.Log($"[CinematicPlayer] DirectorDebugCamera orthographicSize сброшен на {directorCam.defaultSize}");
            }
        }
    }
}