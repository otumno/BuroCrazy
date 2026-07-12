using UnityEngine;
using Managers;

namespace BuroDebug
{
    public class DirectorDebugCamera : MonoBehaviour
    {
        [Header("Настройки переключения")]
        public KeyCode toggleKey = KeyCode.C;
        public float zoomedSize = 3.0f;
        public float defaultSize = 5.4f;
        public float zoomSpeed = 8f;

        [Header("Настройки слежения (Мертвая зона)")]
        [Tooltip("Радиус, внутри которого камера не реагирует на микро-движения")]
        public float deadzoneRadius = 0.5f;
        [Tooltip("Время сглаживания (резиночка). Чем больше, тем 'тяжелее' камера")]
        public float smoothTime = 0.15f;

        [Header("Настройки смещения мыши")]
        [Tooltip("Насколько сильно камера тянется к мыши (0.2 = 20% от расстояния до курсора)")]
        public float mouseOffsetMultiplier = 0.2f;
        [Tooltip("Максимально допустимое смещение камеры от центра (в юнитах)")]
        public float maxMouseOffset = 2.0f;

        [Header("Ограничения карты (Границы)")]
        public bool useBounds = true;
        [Tooltip("Левый нижний угол карты (X, Y)")]
        public Vector2 mapMinBounds = new Vector2(-15f, -10f);
        [Tooltip("Правый верхний угол карты (X, Y)")]
        public Vector2 mapMaxBounds = new Vector2(15f, 10f);

        [Header("Динамический зум")]
        [Tooltip("Насколько отдалять камеру при движении директора")]
        public float movementZoomOffset = 1.0f;
        [Tooltip("Время сглаживания для зума (резиночка)")]
        public float zoomSmoothTime = 0.3f;

        public void SetFollowMode(bool follow)
        {
            _isFollowing = follow;
            if (_cameraToggle != null)
            {
                _cameraToggle.enabled = !_isFollowing;
            }
            if (_isFollowing && DirectorAvatarController.Instance != null)
            {
                _focusPoint = DirectorAvatarController.Instance.transform.position;
                _currentZoomTarget = zoomedSize; // Инициализируем целевой зум при включении слежения
            }
            Debug.Log($"<color=yellow>[DirectorDebugCamera]</color> Режим слежения: {_isFollowing}");
        }

        public void DisableFollowing()
        {
            SetFollowMode(false);
        }

        private Camera _camera;
        private CameraToggle _cameraToggle;
        private bool _isFollowing = false;

        private Vector2 _focusPoint;
        private Vector3 _velocity = Vector3.zero;
        private float _zoomVelocity = 0f;
        private float _currentZoomTarget = 0f; // Текущий целевой зум для инерционного перехода

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _cameraToggle = GetComponent<CameraToggle>();
            Debug.Log($"[DirectorDebugCamera] Awake: _camera={_camera != null}, _cameraToggle={_cameraToggle != null}");
        }

        private void Update()
        {
            // Защита от null _cameraToggle (может быть null если Awake не вызывался после повторного Enable)
            if (_cameraToggle == null)
            {
                _cameraToggle = GetComponent<CameraToggle>();
            }
            
            if (Input.GetKeyDown(toggleKey))
            {
                // Блокируем ВСЁ переключение камеры во время катсцены
                var inputController = FindObjectOfType<PlayerInputController>();
                bool isCutscenePlaying = inputController != null && inputController.IsCutscenePlaying;
                
                if (isCutscenePlaying)
                {
                    Debug.Log($"<color=yellow>[DirectorDebugCamera]</color> Клавиша C заблокирована - идёт катсцена");
                    return;
                }
                
                // Если НЕ в режиме слежения - проверяем, можно ли включить
                if (!_isFollowing)
                {
                    // НЕ включаем слежение если CameraToggle активен (конфликт режимов)
                    if (_cameraToggle != null && _cameraToggle.enabled)
                    {
                        Debug.Log($"<color=yellow>[DirectorDebugCamera]</color> Невозможно включить режим слежения - CameraToggle уже активен");
                        return;
                    }
                    
                    _isFollowing = true;
                    if (_cameraToggle != null)
                    {
                        _cameraToggle.enabled = false;
                    }
                    if (DirectorAvatarController.Instance != null)
                    {
                        _focusPoint = DirectorAvatarController.Instance.transform.position;
                    }
                    Debug.Log($"<color=yellow>[DirectorDebugCamera]</color> Режим слежения ВКЛЮЧЕН");
                }
                else
                {
                    // Выключаем режим слежения
                    _isFollowing = false;
                    if (_cameraToggle != null)
                    {
                        _cameraToggle.enabled = true;
                    }
                    Debug.Log($"<color=yellow>[DirectorDebugCamera]</color> Режим слежения ВЫКЛЮЧЕН, CameraToggle включен");
                }
            }
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            if (_isFollowing)
            {
                if (DirectorAvatarController.Instance == null) return;

                // ПРОВЕРКА: во время катсцены или диалогов отключаем смещение камеры за мышью
                var inputController = FindObjectOfType<PlayerInputController>();
                bool isBlocked = (inputController != null && inputController.IsCutscenePlaying);
                
                // Дополнительная проверка для диалогов: во время диалога тоже блокируем смещение камеры за мышью.
                // Раньше здесь была детекция по тегам (CompareTag("DialogueUI") + FindGameObjectWithTag("MenuPanel")),
                // но тег "MenuPanel" в проекте не определён, из-за чего FindGameObjectWithTag бросал UnityException,
                // а скан дочерних объектов менеджера ничего не находил (у объекта DialogueUIManager нет детей).
                // Используем прямой запрос состояния диалога.
                if (!isBlocked && DialogueUIManager.Instance != null && DialogueUIManager.Instance.IsDialogueActive)
                {
                    isBlocked = true;
                }
                
                // 1. Определяем целевой зум в зависимости от движения директора
                bool isMoving = DirectorAvatarController.Instance.AgentMover != null
                    && DirectorAvatarController.Instance.AgentMover.IsMoving();
                float idealZoom = isMoving ? zoomedSize + movementZoomOffset : zoomedSize;
                
                // 2. Плавное изменение зума через SmoothDamp
                float currentZoomTarget = Mathf.SmoothDamp(_currentZoomTarget, idealZoom, ref _zoomVelocity, zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
                _currentZoomTarget = currentZoomTarget;
                _camera.orthographicSize = _currentZoomTarget;

                // 3. Логика Мертвой зоны (Deadzone)
                Vector2 directorPos = DirectorAvatarController.Instance.transform.position;
                float distanceToDirector = Vector2.Distance(_focusPoint, directorPos);
                
                if (distanceToDirector > deadzoneRadius)
                {
                    Vector2 direction = (directorPos - _focusPoint).normalized;
                    _focusPoint = directorPos - direction * deadzoneRadius;
                }

                // 4. Смещение к курсору мыши (ОТКЛЮЧАЕМ во время катсцен/диалогов!)
                Vector3 targetPos = _focusPoint;
                if (!isBlocked)
                {
                    Vector3 mouseScreenPos = Input.mousePosition;
                    mouseScreenPos.z = Mathf.Abs(_camera.transform.position.z);
                    Vector2 mouseWorldPos = _camera.ScreenToWorldPoint(mouseScreenPos);
                    
                    Vector2 mouseOffset = (mouseWorldPos - _focusPoint) * mouseOffsetMultiplier;
                    mouseOffset = Vector2.ClampMagnitude(mouseOffset, maxMouseOffset);

                    targetPos = _focusPoint + mouseOffset;
                }

                // 5. Ограничение камеры рамками карты (Clamping)
                if (useBounds)
                {
                    float camHalfHeight = _camera.orthographicSize;
                    float camHalfWidth = camHalfHeight * _camera.aspect;

                    float clampedX = Mathf.Clamp(targetPos.x, mapMinBounds.x + camHalfWidth, mapMaxBounds.x - camHalfWidth);
                    float clampedY = Mathf.Clamp(targetPos.y, mapMinBounds.y + camHalfHeight, mapMaxBounds.y - camHalfHeight);
                    
                    targetPos = new Vector3(clampedX, clampedY, targetPos.z);
                }

                targetPos.z = _camera.transform.position.z; // Фиксируем Z

                // 6. Финальная резиночка (SmoothDamp)
                _camera.transform.position = Vector3.SmoothDamp(
                    _camera.transform.position,
                    targetPos,
                    ref _velocity,
                    smoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime
                );
            }
            else
            {
                _camera.orthographicSize = Mathf.SmoothDamp(_camera.orthographicSize, defaultSize, ref _zoomVelocity, zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            }
        }
    }
}