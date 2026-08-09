using Audio;
using Scriptables.Audio;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// Проигрывает зациклённый эмбиент толпы (<see cref="SoundID.Crowd"/>) и плавно регулирует
    /// его громкость по количеству клиентов в офисе: _minVolume при клиентах ≤ _minCount и
    /// вплоть до _maxVolume при клиентах ≥ _maxCount.
    /// </summary>
    public class CrowdSoundManager : MonoBehaviour
    {
        [Header("Громкость")]
        [SerializeField]
        private float _minVolume = 0f;
        [SerializeField]
        private float _maxVolume = 1f;
        [SerializeField]
        private int _minCount = 4;
        [SerializeField]
        private int _maxCount = 12;

        [Header("Сглаживание и опрос")]
        [Tooltip("Скорость изменения громкости, единиц в секунду (0 — менять мгновенно).")]
        [SerializeField]
        private float _volumeChangeSpeed = 1f;
        [Tooltip("Как часто пересчитывать количество клиентов, сек.")]
        [SerializeField]
        private float _recountInterval = 0.5f;

        [Header("Звук")]
        [SerializeField]
        private SoundID _soundId = SoundID.Crowd;

        private float _currentVolume;
        private float _targetVolume;
        private float _recountTimer;
        private AudioInstance _audioInstance;

        private void Start()
        {
            if (AudioManager.Instance == null)
            {
                Debug.LogError("[CrowdSoundManager] AudioManager.Instance не найден на сцене.");
                enabled = false;
                return;
            }

            var library = AudioManager.Instance.soundLibrary;
            var soundData = library != null ? library.GetSound(_soundId) : null;
            if (soundData == null)
            {
                Debug.LogError($"[CrowdSoundManager] Звук {_soundId} не найден в SoundLibrary.");
                enabled = false;
                return;
            }

            if (!soundData.Loop)
            {
                Debug.LogError($"[CrowdSoundManager] У звука {_soundId} выключен Loop — включи его в SoundLibrary!");
                return;
            }

            _currentVolume = _targetVolume = CalculateTargetVolume(GetClientCount());

            _audioInstance = AudioManager.Instance.PlaySound(_soundId);
            _audioInstance?.SetVolume(_currentVolume);
        }

        private void Update()
        {
            if (_audioInstance == null)
                return;

            _recountTimer -= Time.deltaTime;
            if (_recountTimer <= 0f)
            {
                _recountTimer = _recountInterval;
                _targetVolume = CalculateTargetVolume(GetClientCount());
            }

            _currentVolume = _volumeChangeSpeed > 0f
                ? Mathf.MoveTowards(_currentVolume, _targetVolume, _volumeChangeSpeed * Time.deltaTime)
                : _targetVolume;

            _audioInstance.SetVolume(_currentVolume);
        }

        private float CalculateTargetVolume(int clientCount)
        {
            var t = Mathf.InverseLerp(_minCount, _maxCount, clientCount);
            return Mathf.Lerp(_minVolume, _maxVolume, t);
        }

        private static int GetClientCount() => ClientSpawnerWithArchetypes.Instance?.GetActiveClientCount() ?? 0;

        private void OnDestroy() => _audioInstance?.Stop(instant: true);
    }
}
