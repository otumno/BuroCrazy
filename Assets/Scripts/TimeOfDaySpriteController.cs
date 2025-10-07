using UnityEngine;
using System.Collections.Generic;

public class TimeOfDaySpriteController : MonoBehaviour
{
    [Tooltip("Перетащите сюда все 2D спрайты, цвет которых должен меняться")]
    public List<SpriteRenderer> tintedSprites;
    
    private PeriodSettings currentPeriodPlan;
    private PeriodSettings previousPeriodPlan;
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
        if (tintedSprites == null || tintedSprites.Count == 0 || ClientSpawner.Instance == null) return;
        
        currentPeriodPlan = ClientSpawner.Instance.GetCurrentPeriodPlan();
        previousPeriodPlan = ClientSpawner.Instance.GetPreviousPeriodPlan();
        periodTimer = ClientSpawner.Instance.GetPeriodTimer();

        if (currentPeriodPlan == null || previousPeriodPlan == null) return;
        
        // FIX: Evaluate the AnimationCurve to get the duration for the current day.
        float duration = currentPeriodPlan.durationInSeconds.Evaluate(ClientSpawner.Instance.GetCurrentDay());
        if (duration <= 0) return; // Avoid division by zero

        // Плавно вычисляем нужный цвет
        float progress = Mathf.Clamp01(periodTimer / duration);
        Color targetColor = Color.Lerp(previousPeriodPlan.panelColor, currentPeriodPlan.panelColor, progress);

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