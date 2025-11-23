using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Data.Calendar;
using Managers;

// todo: this is okay, actually
[System.Serializable]
public class PeriodVisual
{
    public string periodName;
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

    private CalendarDayPeriodType _periodType;

    private void Awake()
    {
        clockImage = GetComponent<Image>();
        audioSource ??= GetComponent<AudioSource>();
    }

    // todo: subscribe ui
    private void OnEnable()
    {
        // if (ClientSpawner.Instance != null)
        //     ClientSpawner.Instance.OnPeriodChanged += UpdateClock;
        //
        // UpdateClock();
    }

    private void OnDisable()
    {
        if (ClientSpawner.Instance != null)
        {
            ClientSpawner.Instance.OnPeriodChanged -= UpdateClock;
        }
    }

    // todo: UI SUBSCRIPTION!!!!!!! fix this
    private void UpdateClock()
    {
        // if (ClientSpawner.Instance == null)
        //     return;
        //
        // var currentPeriodType = ClientSpawner.CurrentPeriodType;
        // if (string.IsNullOrEmpty(currentPeriodType))
        //     return;
        //
        // if (currentPeriodType == _periodType)
        //     return;
        //
        // // Ищем настройку для нового периода
        // PeriodVisual currentVisual = periodVisuals.FirstOrDefault(v => v.periodName.Equals(currentPeriodType, System.StringComparison.InvariantCultureIgnoreCase));
        //
        // if (currentVisual != null)
        // {
        //     // Обновляем иконку
        //     if (currentVisual.icon != null)
        //         clockImage.sprite = currentVisual.icon;
        //
        //     // Проигрываем звук, ТОЛЬКО ЕСЛИ это не первый запуск (lastShownPeriodName уже был установлен)
        //     if (currentVisual.transitionSound != null && audioSource != null && _periodType != null)
        //         audioSource.PlayOneShot(currentVisual.transitionSound);
        // }
        //
        // _periodType = currentPeriodType;
    }
}