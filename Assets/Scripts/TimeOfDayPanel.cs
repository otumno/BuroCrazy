// Файл: TimeOfDayPanel.cs
using UnityEngine;
using UnityEngine.UI;

public class TimeOfDayPanel : MonoBehaviour
{
    [Tooltip("Перетащите сюда компонент Image с этой же панели")]
    public Image panelImage;

    private SpawningPeriod currentPeriod;
    private SpawningPeriod previousPeriod;
    private float periodTimer;

    void Start()
    {
        if (panelImage == null)
        {
            panelImage = GetComponent<Image>();
        }
        if (ClientSpawner.Instance == null)
        {
            Debug.LogError("TimeOfDayPanel не может найти ClientSpawner!");
            enabled = false;
        }
    }

    void Update()
    {
        if (panelImage == null || ClientSpawner.Instance == null || ClientSpawner.Instance.periods.Length == 0) return;

        // Получаем текущие данные из ClientSpawner'а
        currentPeriod = ClientSpawner.Instance.GetCurrentPeriod();
        previousPeriod = ClientSpawner.Instance.GetPreviousPeriod();
        periodTimer = ClientSpawner.Instance.GetPeriodTimer();

        if (currentPeriod == null || previousPeriod == null) return;
        
        // Плавно меняем цвет панели, используя ту же логику, что и для глобального освещения
        float progress = Mathf.Clamp01(periodTimer / currentPeriod.durationInSeconds);
        panelImage.color = Color.Lerp(previousPeriod.panelColor, currentPeriod.panelColor, progress);
    }
}