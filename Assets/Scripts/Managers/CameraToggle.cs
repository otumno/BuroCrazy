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

        [Header("Настройки снапа")]
        [SerializeField] private float _speedSnap = 15f;
        [SerializeField] private float _snapDelay = 0.5f;
        [SerializeField] private float _snapDistance = 0.5f;
        
        [Header("Настройки скролла")]
        [SerializeField] private float _speedScroll = 1000f;
        
        [Header("Управление")]
        [SerializeField] private string _scrollAxis = "Mouse ScrollWheel";
        [SerializeField] private KeyCode _keyCode = KeyCode.Tab;

        // оставил список, чтобы можно было подглядеть, что там было.
        // больше будет не нужен, если то что внутри игрового мира в Canvas World UI перенести
        [Header("UI для переключения (Черный список)")]
        public List<GameObject> allToggleableUI;

        private readonly ActiveRecord<bool> _shouldMuffle = new();

        private int? _targetIndex;
        private float _timeBeforeSnap;

        private void Awake()
        {
            _positions = _positions.OrderBy(t => t.position.y).ToArray();
            _camera.transform.position = _positions[0].position;
            _shouldMuffle.Subscribe(OnShouldMuffleChange);
            MusicPlayer.Instance.SetMuffled(false);
        }

        private void LateUpdate()
        {
            var scrollInput = 0f;
            var hasTabInput = false;
            var deltaTime = Time.timeScale > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
            
            if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                scrollInput = Input.GetAxis(_scrollAxis);
                hasTabInput = Input.GetKeyDown(_keyCode);
            }

            if (hasTabInput)
                TogglePosition();

            if (scrollInput != 0f)
            {
                _targetIndex = null;
                _timeBeforeSnap = _snapDelay;
                
                
                var offset = deltaTime * _speedScroll * scrollInput;

                var cameraTransform = _camera.transform;
                var currentPosition = cameraTransform.position;
                var newY = Mathf.Clamp(currentPosition.y + offset, _positions[0].position.y, _positions[^1].position.y);
                var newPosition = new Vector3(currentPosition.x, newY, currentPosition.z);
                cameraTransform.position = newPosition;
            }

            _timeBeforeSnap -= deltaTime;
            if (_timeBeforeSnap <= 0f && !_targetIndex.HasValue && _snapDistance > 0f)
            {
                var (closestIndex, shouldSnap) = GetClosestIndex(_camera.transform.position, _positions, _snapDistance);
                
                if (shouldSnap)
                    _targetIndex = closestIndex;
            }

            if (_targetIndex.HasValue)
            {
                var targetPosition = _positions[_targetIndex.Value].position;
                _camera.transform.position = Vector3.Lerp(_camera.transform.position, targetPosition, deltaTime * _speedSnap);
            }

            _shouldMuffle.Value = GetShouldMuffle();
        }

        public void TogglePosition()
        {
            if (!_targetIndex.HasValue)
                (_targetIndex, _) = GetClosestIndex(_camera.transform.position, _positions, _snapDistance);
            else
            {
                _targetIndex++;
                
                if (_targetIndex >= _positions.Length)
                    _targetIndex = 0;
            }
        }

        private bool GetShouldMuffle()
        {
            var cameraPosition = _camera.transform.position; 
            var distanceToFirst = Vector3.SqrMagnitude(cameraPosition - _positions[0].position);
            var distanceToSecond = Vector3.SqrMagnitude(cameraPosition - _positions[1].position);
            return distanceToFirst > distanceToSecond;
        }

        private static void OnShouldMuffleChange(bool shouldMuffle, bool _) =>
            MusicPlayer.Instance.SetMuffled(shouldMuffle);

        private static (int closestIndex, bool shouldSnap) GetClosestIndex(Vector3 cameraPosition,
                                                                           IReadOnlyList<Transform> positions,
                                                                           float snapDistance)
        {
            var result = -1;
            var minDistance = float.MaxValue;
            var shouldSnap = false;
            for (int k = 0; k < positions.Count; k++)
            {
                var position = positions[k].position;
                var distance = Vector3.SqrMagnitude(cameraPosition - position);
                
                if (!(distance < minDistance))
                    continue;
                
                result = k;
                minDistance = distance;
                shouldSnap = minDistance < snapDistance;
            }

            Debug.Assert(result != -1, "result != -1");
            return (result, shouldSnap);
        }
        
        private void OnDestroy()
        {
            _shouldMuffle.Dispose();
        }
    }
}