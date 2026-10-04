using System;
using DG.Tweening;
using DoTweenExt;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class SwitchToggle : MonoBehaviour
    {
        [Header("Operational")]
        [SerializeField]
        private Toggle _toggle;
        [SerializeField]
        private Image _trackImage;
        [SerializeField]
        private RectTransform _knobRectTransform;
        
        [Header("Visuals")]
        [SerializeField]
        private Color _onColor = new Color(0.204f, 0.78f, 0.349f, 1f);
        [SerializeField]
        private Color _offColor = new Color(0.914f, 0.914f, 0.918f, 1f);
        
        [Header("Layout")]
        [SerializeField]
        private float _knobPadding = 5.4f;

        [Header("Animation")]
        [SerializeField]
        private float _duration = 0.2f;

        private float _knobPosition;
        private Sequence _switchSequence;
        private Action<bool> _callback;

        public bool IsOn => _toggle.isOn;

        /// <summary>
        /// Единственный подписчик на переключение игроком. Повторный вызов заменяет предыдущего.
        /// </summary>
        public void SetListener(Action<bool> callback)
        {
            _callback = callback;
        }

        public void SetInteractable(bool isInteractable)
        {
            _toggle.interactable = isInteractable;
        }

        public void SetState(bool isOn, bool silent)
        {
            if (silent)
            {
                _toggle.SetIsOnWithoutNotify(isOn);
                ApplyState(isOn, instant: true);
            }
            else
            {
                _toggle.isOn = isOn;
            }
        }

        private void Awake()
        {
            _toggle.onValueChanged.AddListener(OnToggleValueChanged);
        }

        private void OnEnable()
        {
            ApplyState(_toggle.isOn, instant: true);
        }

        private void OnDestroy()
        {
            _toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
        }

        private void OnToggleValueChanged(bool isOn)
        {
            ApplyState(isOn, instant: false);
            _callback?.Invoke(isOn);
        }

        private void ApplyState(bool isOn, bool instant = false)
        {
            float targetKnobPosition = isOn ? 1f : 0f;
            Color trackColor = isOn ? _onColor : _offColor;
            float duration = GetDuration();

            _switchSequence.SafeKill();

            if (instant)
            {
                SetKnobPosition(targetKnobPosition);
                _trackImage.color = trackColor;
                return;
            }

            _switchSequence = DOTween.Sequence()
                .Join(DOTween.To(() => _knobPosition, SetKnobPosition, targetKnobPosition, duration)
                    .SetEase(Ease.OutCubic))
                .Join(_trackImage.DOColor(trackColor, duration))
                .SetUpdate(isIndependentUpdate: true)
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        }

        private float GetDuration()
        {
            if (!_switchSequence.IsActive() || !_switchSequence.IsPlaying())
                return _duration;

            return Mathf.Min(_duration, _switchSequence.Elapsed());
        }

        private void SetKnobPosition(float position)
        {
            _knobPosition = position;
            _knobRectTransform.anchorMin = new Vector2(position, 0f);
            _knobRectTransform.anchorMax = new Vector2(position, 1f);
            _knobRectTransform.pivot = new Vector2(position, 0.5f);
            _knobRectTransform.anchoredPosition = new Vector2(_knobPadding * (1f - 2f * position), 0f);
            _knobRectTransform.sizeDelta = new Vector2(_knobRectTransform.sizeDelta.x, -2f * _knobPadding);
        }
    }
}
