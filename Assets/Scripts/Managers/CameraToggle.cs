// Файл: CameraToggle.cs

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utilities;

namespace Managers
{
    public class CameraToggle : MonoBehaviour
    {
        [Header("Камера и точки")]
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform[] _positions;

        [Header("Настройки перехода")]
        [SerializeField] private float _transitionSpeed = 5f;
        
        [Header("Управление")]
        [SerializeField] private string _scrollAxis = "Mouse ScrollWheel";
        [SerializeField] private KeyCode _keyCode = KeyCode.Tab;

        private readonly ActiveRecord<bool> _shouldMuffle = new();

        private int? _targetIndex;
        private float? _targetY;

        private void Awake()
        {
            _positions = _positions.OrderBy(t => t.position.y).ToArray();
            if (_positions.Length > 0)
                _camera.transform.position = _positions[0].position;
            _shouldMuffle.Subscribe(OnShouldMuffleChange);
            MusicPlayer.Instance.SetMuffled(false);
        }

        private void Update()
        {
            var scrollInput = 0f;
            var hasTabInput = false;
            var deltaTime = Time.unscaledDeltaTime;
            
            if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                scrollInput = Input.GetAxis(_scrollAxis);
                hasTabInput = Input.GetKeyDown(_keyCode);
            }

            // Tab - переключение по кругу
            if (hasTabInput)
                ToggleToNext();

            // Скролл вниз → нижняя точка
            if (scrollInput > 0.01f)
                _targetIndex = _positions.Length - 1;
            
            // Скролл вверх → верхняя точка
            if (scrollInput < -0.01f)
                _targetIndex = 0;

            // Плавное движение к цели
            if (_targetIndex.HasValue && _positions.Length > _targetIndex.Value)
            {
                var targetPos = _positions[_targetIndex.Value].position;
                var newPos = Vector3.MoveTowards(_camera.transform.position, targetPos, deltaTime * _transitionSpeed);
                _camera.transform.position = newPos;

                // Если достигли цели - сбрасываем
                if (Vector3.SqrMagnitude(_camera.transform.position - targetPos) < 0.001f)
                    _targetIndex = null;
            }

            _shouldMuffle.Value = GetShouldMuffle();
        }

        public void ResetToDefault()
        {
            if (_positions.Length > 0)
            {
                _targetIndex = 0;
                _camera.transform.position = _positions[0].position;
            }
            Debug.Log("[CameraToggle] Reset to default position");
        }

        private void ToggleToNext()
        {
            if (_positions.Length == 0) return;
            
            var currentPos = _camera.transform.position;
            var currentY = currentPos.y;
            
            // Находим текущую позицию относительно точек
            int currentIndex = 0;
            float minDist = float.MaxValue;
            for (int i = 0; i < _positions.Length; i++)
            {
                var dist = Mathf.Abs(_positions[i].position.y - currentY);
                if (dist < minDist)
                {
                    minDist = dist;
                    currentIndex = i;
                }
            }
            
            int next = currentIndex + 1;
            if (next >= _positions.Length) next = 0;
            _targetIndex = next;
        }

        private bool GetShouldMuffle()
        {
            if (_positions.Length < 2) return false;
            var cameraPosition = _camera.transform.position; 
            var distanceToFirst = Vector3.SqrMagnitude(cameraPosition - _positions[0].position);
            var distanceToSecond = Vector3.SqrMagnitude(cameraPosition - _positions[1].position);
            return distanceToFirst > distanceToSecond;
        }

        private static void OnShouldMuffleChange(bool shouldMuffle, bool _) =>
            MusicPlayer.Instance.SetMuffled(shouldMuffle);
        
        private void OnDestroy()
        {
            _shouldMuffle.Dispose();
        }
    }
}
