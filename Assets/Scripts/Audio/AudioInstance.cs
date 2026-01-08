using DG.Tweening;
using DoTweenExt;
using Scriptables.Audio;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio
{
    public class AudioInstance : MonoBehaviour
    {
        public bool isPlaying => _audioSource.isPlaying;
        public bool isBusy => _isReserved || _audioSource.isPlaying;

        public SoundData soundData => _soundData;
        public AudioMixerGroup audioGroup => _audioSource.outputAudioMixerGroup;

        // runtime
        private float _pitch;
        private bool _isReserved;
        private SoundData _soundData;
        private Transform _linkTransform;
        private Sequence _volumeSequence;

        // init
        private AudioSource _audioSource;

        public void Init(AudioSource audioSource) => _audioSource = audioSource;

        public void Play(SoundData soundData,
                         Transform linkTransform,
                         Vector3? position)
        {
            if (_audioSource.isPlaying)
            {
                Debug.LogError($"AudioSource: {name} isBusy!");
                return;
            }
            
            ResetSource();

            _soundData = soundData;
            _linkTransform = linkTransform;

            var startPosition = Vector3.zero;;
            if (position != null)
            {
                startPosition = position.Value;
            }
            else if (linkTransform != null)
            {
                startPosition = linkTransform.position;
            }
            transform.position = startPosition;

            var randomAudioClipIndex = Random.Range(0, soundData.clips.Length);
            _audioSource.clip = soundData.clips[randomAudioClipIndex];
            _audioSource.volume = soundData.volume;
            _audioSource.outputAudioMixerGroup = soundData.mixerGroup;
            _audioSource.loop = soundData.Loop;
            _audioSource.priority = soundData.Priority;

            // pitch
            var pitchDelta = Random.Range(-soundData.pitchDelta, soundData.pitchDelta);
            _audioSource.pitch = soundData.pitch + pitchDelta;
            
            // 2d / 3d
            _audioSource.spatialBlend = soundData.SpatialBlend;

            // 3d settings
            _audioSource.rolloffMode = soundData.Rolloff;
            _audioSource.dopplerLevel = soundData.DopplerLevel;
            _audioSource.minDistance = soundData.DistanceMin;
            _audioSource.maxDistance = soundData.DistanceMax;

            _volumeSequence.SafeKill();
            if (_soundData.FadeInDuration > 0)
            {
                _volumeSequence = _audioSource.AudioFadeIn(_soundData.FadeInDuration,
                                                           _audioSource.volume,
                                                           _soundData.FadeInEase);
            }

            _audioSource.Play();
        }

        public void PlayAudioClip2D(AudioClip clip, AudioMixerGroup mixerGroup)
        {
            if (clip == null)
                return;

            _audioSource.clip = clip;
            _audioSource.outputAudioMixerGroup = mixerGroup;
            _audioSource.spatialBlend = 0f;
        }

        public void PlayVoiceClip(AudioClip clip,
                                  Vector3 position,
                                  float basePitch,
                                  float pitchDelta,
                                  float volume,
                                  AudioMixerGroup sfxGroup)
        {
            if (clip == null)
                return;

            transform.position = position;
            _audioSource.clip = clip;
            _audioSource.outputAudioMixerGroup = sfxGroup;

            float randomPitch = Random.Range(-pitchDelta, pitchDelta);
            _audioSource.pitch = Mathf.Clamp(basePitch + randomPitch, 0.1f, 3f);

            _audioSource.volume = Mathf.Clamp01(volume);
            _audioSource.spatialBlend = 1f;

            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 15f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;

            _audioSource.Play();
        }

        public void ResetSource()
        {
            _audioSource.mute = false;
            _audioSource.loop = false;
            _audioSource.priority = 128;
            _audioSource.pitch = 1f;
            _audioSource.spatialBlend = 1f;
            _audioSource.dopplerLevel = 1f;
            _audioSource.minDistance = 0f;
            _audioSource.maxDistance = 50f;
        }

        public void Continue()
        {
            _isReserved = false;
            _volumeSequence?.Play();
            _audioSource.Play();
        }

        public void Pause()
        {
            _isReserved = true;
            _volumeSequence?.Pause();
            _audioSource.Pause();
        }

        public void Stop(bool instant)
        {
            _isReserved = false;
            _volumeSequence.SafeKill();

            if (instant || soundData.FadeOutDuration <= 0)
            {
                _audioSource.Stop();
            }
            else
            {
                _volumeSequence = _audioSource
                    .AudioFadeOut(_soundData.FadeOutDuration, _soundData.FadeOutEase);
            }

            _soundData = null;
            _linkTransform = null;
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (!_audioSource.isPlaying || !_linkTransform)
                return;

            transform.position = _linkTransform.position;
        }
    }
}