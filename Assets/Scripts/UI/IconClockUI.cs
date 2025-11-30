using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar; // Обязательно для CalendarDayPeriodType
using Managers;

namespace UI
{
    // Класс для настройки визуалов в инспекторе
    [System.Serializable]
    public class PeriodVisual
    {
        [Tooltip("Выберите тип периода из списка")]
        public CalendarDayPeriodType periodType; // Теперь используем Enum, а не строку
        public Sprite icon;
        public AudioClip transitionSound;
    }

    [RequireComponent(typeof(Image), typeof(AudioSource))]
    public class IconClockUI : MonoBehaviour
    {
        [Header("Визуальные элементы периодов")]
        public List<PeriodVisual> periodVisuals;

        [Header("Ссылки")]
        [SerializeField] private AudioSource audioSource;
        private Image clockImage;

        // Храним последний тип, чтобы не спамить обновлениями
        private CalendarDayPeriodType _lastPeriodType = CalendarDayPeriodType.None;

        private void Start()
        {
            clockImage = GetComponent<Image>();
            audioSource ??= GetComponent<AudioSource>();
            
            if (DayPeriodManager.Instance == null)
            {
                Debug.LogError($"DayPeriodManager == null!!");
                return;
            }
            
            DayPeriodManager.Instance.OnPeriodChanged += UpdateClock;
            UpdateClock();
        }

        private void UpdateClock()
        {
            if (DayPeriodManager.Instance == null)
                return;

            // Получаем текущий тип периода напрямую из Enum
            var currentPeriodType = DayPeriodManager.Instance.CurrentPeriodType;

            // Если период не изменился с прошлого раза, ничего не делаем
            if (currentPeriodType == _lastPeriodType) return;

            // Ищем настройку в списке по Enum
            PeriodVisual currentVisual = periodVisuals.FirstOrDefault(v => v.periodType.HasFlag(currentPeriodType));

            if (currentVisual != null)
            {
                // Обновляем иконку
                if (currentVisual.icon != null && clockImage != null)
                {
                    clockImage.sprite = currentVisual.icon;
                }

                // Проигрываем звук (если назначен и это не инициализация "None")
                if (currentVisual.transitionSound != null && audioSource != null && _lastPeriodType != CalendarDayPeriodType.None)
                {
                    audioSource.PlayOneShot(currentVisual.transitionSound);
                }
            }
            else
            {
                // Полезный лог, если забыл настроить иконку для периода
                Debug.LogWarning($"[IconClockUI] Не найдена иконка для периода: {currentPeriodType}");
            }

            // Запоминаем текущий период
            _lastPeriodType = currentPeriodType;
        }
        
        private void OnDestroy()
        {
            DayPeriodManager.Instance.OnPeriodChanged -= UpdateClock;
        }
    }
}