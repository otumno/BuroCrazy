using UnityEngine;
using System.Collections.Generic;
using Data.Calendar;
using Managers;

public class TimeOfDaySpriteController : MonoBehaviour
{
    public List<SpriteRenderer> tintedSprites;
    private PeriodSettings currentPeriodPlan;
    private PeriodSettings previousPeriodPlan;
    private float periodTimer;

    private readonly Dictionary<SpriteRenderer, float> _originalAlphas = new();

    private void Start()
    {
        // Сохраняем исходную прозрачность
        _originalAlphas.Clear();
        foreach (var sprite in tintedSprites)
        {
            if (sprite != null) _originalAlphas[sprite] = sprite.color.a;
        }
    }

    private void Update()
    {
        if (tintedSprites == null || tintedSprites.Count == 0 || TimeManager.Instance == null)
            return;
        
        // --- ИСПРАВЛЕНИЕ: Берем данные из TimeManager ---
        currentPeriodPlan = TimeManager.Instance.GetCurrentPeriodSettings();
        previousPeriodPlan = TimeManager.Instance.GetPreviousPeriodSettings();
        periodTimer = TimeManager.Instance.GetPeriodTimer();
        // -----------------------------------------------

        if (currentPeriodPlan == null || previousPeriodPlan == null) return;

        var duration = currentPeriodPlan.durationInSeconds;
        if (duration <= 0) return;

        float progress = Mathf.Clamp01(periodTimer / duration);
        
        Color prevColor = previousPeriodPlan.panelColor;
        Color currentColor = currentPeriodPlan.panelColor;

        Color targetColorRGB = Color.Lerp(prevColor, currentColor, progress);

        foreach (var sprite in tintedSprites)
        {
            if (sprite != null)
            {
                float originalAlpha = _originalAlphas.ContainsKey(sprite) ? _originalAlphas[sprite] : 1f;
                sprite.color = new Color(targetColorRGB.r, targetColorRGB.g, targetColorRGB.b, originalAlpha);
            }
        }
    }
}