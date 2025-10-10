using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class PeriodVisual
{
    public string periodName;
    public Sprite icon;
    public AudioClip transitionSound;
}

[RequireComponent(typeof(Image))]
public class IconClockUI : MonoBehaviour
{
    [Header("Визуальные элементы периодов")]
    public List<PeriodVisual> periodVisuals;

    [Header("Ссылки")]
    [SerializeField] private AudioSource audioSource;
    private Image clockImage;

    // --- НАЧАЛО ИЗМЕНЕНИЙ ---
    // Переменная для хранения имени ПОСЛЕДНЕГО отображенного периода
    private string lastShownPeriodName = null;
    // --- КОНЕЦ ИЗМЕНЕНИЙ ---

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
        if (ClientSpawner.Instance != null)
        {
            ClientSpawner.Instance.OnPeriodChanged += UpdateClock;
        }
        // Сразу обновляем часы при включении
        UpdateClock();
    }

    private void OnDisable()
    {
        if (ClientSpawner.Instance != null)
        {
            ClientSpawner.Instance.OnPeriodChanged -= UpdateClock;
        }
    }

    private void UpdateClock()
    {
        if (ClientSpawner.Instance == null) return;

        string currentPeriodName = ClientSpawner.CurrentPeriodName;
        if (string.IsNullOrEmpty(currentPeriodName)) return;

        // --- НАЧАЛО НОВОЙ ЛОГИКИ ---

        // Если текущий период уже отображается, ничего не делаем
        if (currentPeriodName == lastShownPeriodName) return;

        // Ищем настройку для нового периода
        PeriodVisual currentVisual = periodVisuals.FirstOrDefault(v => v.periodName.Equals(currentPeriodName, System.StringComparison.InvariantCultureIgnoreCase));

        if (currentVisual != null)
        {
            // Обновляем иконку
            if (currentVisual.icon != null)
            {
                clockImage.sprite = currentVisual.icon;
            }

            // Проигрываем звук, ТОЛЬКО ЕСЛИ это не первый запуск (lastShownPeriodName уже был установлен)
            if (currentVisual.transitionSound != null && audioSource != null && lastShownPeriodName != null)
            {
                audioSource.PlayOneShot(currentVisual.transitionSound);
            }
        }

        // Запоминаем, какой период мы только что показали
        lastShownPeriodName = currentPeriodName;
        // --- КОНЕЦ НОВОЙ ЛОГИКИ ---
    }
}