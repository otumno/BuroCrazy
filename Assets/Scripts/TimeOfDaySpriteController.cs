// Файл: TimeOfDaySpriteController.cs
using UnityEngine;
using System.Collections.Generic;

public class TimeOfDaySpriteController : MonoBehaviour
{
    [Tooltip("Перетащите сюда все 2D спрайты, цвет которых должен меняться в зависимости от времени суток")]
    public List<SpriteRenderer> tintedSprites;

    private SpawningPeriod currentPeriod;
    private SpawningPeriod previousPeriod;
    private float periodTimer;

    void Start()
    {
        if (ClientSpawner.Instance == null)
        {
            Debug.LogError("TimeOfDaySpriteController не может найти ClientSpawner!");
            enabled = false;
        }
    }

    void Update()
    {
        if (tintedSprites == null || tintedSprites.Count == 0 || ClientSpawner.Instance == null || ClientSpawner.Instance.periods.Length == 0) return;

        // Получаем текущие данные из ClientSpawner'а
        currentPeriod = ClientSpawner.Instance.GetCurrentPeriod();
        previousPeriod = ClientSpawner.Instance.GetPreviousPeriod();
        periodTimer = ClientSpawner.Instance.GetPeriodTimer();

        if (currentPeriod == null || previousPeriod == null) return;
        
        // Плавно вычисляем нужный цвет
        float progress = Mathf.Clamp01(periodTimer / currentPeriod.durationInSeconds);
        Color targetColor = Color.Lerp(previousPeriod.panelColor, currentPeriod.panelColor, progress);

        // Применяем вычисленный цвет ко всем спрайтам в списке
        foreach (var sprite in tintedSprites)
        {
            if (sprite != null)
            {
                sprite.color = targetColor;
            }
        }
    }
}