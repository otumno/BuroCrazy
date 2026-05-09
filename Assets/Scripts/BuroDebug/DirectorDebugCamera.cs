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
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _isFollowing = !_isFollowing;

                if (_cameraToggle != null)
                {
                    _cameraToggle.enabled = !_isFollowing;
                }

                if (_isFollowing && DirectorAvatarController.Instance != null)
                {
                    _focusPoint = DirectorAvatarController.Instance.transform.position;
                }

                Debug.Log($"<color=yellow>[DirectorDebugCamera]</color> Режим слежения: {_isFollowing}");
            }
        }

        private void LateUpdate()
        {
            if (_camera == null) return;

            if (_isFollowing)
            {
                if (DirectorAvatarController.Instance == null) return;

                // 1. Определяем целевой зум в зависимости от движения директора
                bool isMoving = DirectorAvatarController.Instance.AgentMover != null
                    && DirectorAvatarController.Instance.AgentMover.IsMoving();
                float idealZoom = isMoving ? zoomedSize + movementZoomOffset : zoomedSize;
                
                // 2. Плавное изменение зума через SmoothDamp (теперь сам targetZoom меняется плавно)
                // Используем промежуточную переменную _currentZoomTarget для инерционного перехода
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

                // 3. Смещение к курсору мыши
                Vector3 mouseScreenPos = Input.mousePosition;
                mouseScreenPos.z = Mathf.Abs(_camera.transform.position.z);
                Vector2 mouseWorldPos = _camera.ScreenToWorldPoint(mouseScreenPos);
                
                Vector2 mouseOffset = (mouseWorldPos - _focusPoint) * mouseOffsetMultiplier;
                mouseOffset = Vector2.ClampMagnitude(mouseOffset, maxMouseOffset);

                // 4. Предварительная цель
                Vector3 targetPos = _focusPoint + mouseOffset;

                // 5. Ограничение камеры рамками карты (Clamping)
                if (useBounds)
                {
                    // Динамически высчитываем размеры половины экрана
                    float camHalfHeight = _camera.orthographicSize;
                    float camHalfWidth = camHalfHeight * _camera.aspect;

                    // Ограничиваем так, чтобы край экрана не вылезал за границы mapMinBounds и mapMaxBounds
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