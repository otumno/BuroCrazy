using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

// Эта структура поможет нам в инспекторе связать имя периода, иконку и звук.
[System.Serializable]
public class PeriodVisual
{
    public string periodName; // Например, "Утро", "День"
    public Sprite icon;
    public AudioClip transitionSound;
}

[RequireComponent(typeof(Image))]
public class IconClockUI : MonoBehaviour
{
    [Header("Визуальные элементы периодов")]
    [Tooltip("Заполните этот список для каждого периода в игре")]
    public List<PeriodVisual> periodVisuals;

    [Header("Ссылки")]
    [Tooltip("AudioSource для проигрывания звуков. Если пусто, будет искаться на этом же объекте.")]
    [SerializeField] private AudioSource audioSource;
    private Image clockImage;

    private void Awake()
    {
        clockImage = GetComponent<Image>();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        // Подписываемся на событие смены периода
        if (ClientSpawner.Instance != null)
        {
            ClientSpawner.Instance.OnPeriodChanged += UpdateClock;
        }
        // Сразу обновляем часы при включении
        UpdateClock();
    }

    private void OnDisable()
    {
        // ОБЯЗАТЕЛЬНО отписываемся от события при выключении, чтобы избежать ошибок
        if (ClientSpawner.Instance != null)
        {
            ClientSpawner.Instance.OnPeriodChanged -= UpdateClock;
        }
    }

    /// <summary>
    /// Главный метод, который обновляет иконку и проигрывает звук.
    /// </summary>
    private void UpdateClock()
    {
        if (ClientSpawner.Instance == null) return;

        string currentPeriodName = ClientSpawner.CurrentPeriodName;
        if (string.IsNullOrEmpty(currentPeriodName)) return;

        // Ищем в нашем списке настройку для текущего периода
        PeriodVisual currentVisual = periodVisuals.FirstOrDefault(v => v.periodName.Equals(currentPeriodName, System.StringComparison.InvariantCultureIgnoreCase));

        if (currentVisual != null)
        {
            // Если нашли - обновляем спрайт
            if (currentVisual.icon != null)
            {
                clockImage.sprite = currentVisual.icon;
            }

            // и проигрываем звук (если он есть и источник звука доступен)
            if (currentVisual.transitionSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(currentVisual.transitionSound);
            }
        }
    }
}