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

        private Camera _camera;
        private CameraToggle _cameraToggle;
        private bool _isFollowing = false;

        // Точка, за которой реально следит камера (с учетом мертвой зоны)
        private Vector2 _focusPoint; 
        private Vector3 _velocity = Vector3.zero;

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
                    // При включении мгновенно переносим фокус на директора, чтобы камера не летела через всю карту
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

                // 1. Плавный Зум
                _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, zoomedSize, Time.unscaledDeltaTime * zoomSpeed);

                // 2. Логика Мертвой зоны (Deadzone)
                Vector2 directorPos = DirectorAvatarController.Instance.transform.position;
                float distanceToDirector = Vector2.Distance(_focusPoint, directorPos);
                
                if (distanceToDirector > deadzoneRadius)
                {
                    // Директор "толкает" границу мертвой зоны, смещая точку фокуса
                    Vector2 direction = (directorPos - _focusPoint).normalized;
                    _focusPoint = directorPos - direction * deadzoneRadius;
                }

                // 3. Смещение к курсору мыши
                Vector3 mouseScreenPos = Input.mousePosition;
                mouseScreenPos.z = Mathf.Abs(_camera.transform.position.z); // Глубина для перевода в мировые координаты
                Vector2 mouseWorldPos = _camera.ScreenToWorldPoint(mouseScreenPos);
                
                // Вектор от фокуса до мыши
                Vector2 mouseOffset = (mouseWorldPos - _focusPoint) * mouseOffsetMultiplier;
                // Ограничиваем максимальное смещение
                mouseOffset = Vector2.ClampMagnitude(mouseOffset, maxMouseOffset);

                // 4. Финальная цель и Резиночка (SmoothDamp)
                Vector3 targetPos = _focusPoint + mouseOffset;
                targetPos.z = _camera.transform.position.z; // Жестко фиксируем плоскость Z камеры

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
                // Возвращаем зум в норму, когда выключаем слежение
                _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, defaultSize, Time.unscaledDeltaTime * zoomSpeed);
            }
        }
    }
}