// Файл: Assets/Scripts/TimeOfDaySpriteController.cs
using UnityEngine;
using System.Collections.Generic;
using Data.Calendar;
using Managers;

// todo: link this to TimeSystem
public class TimeOfDaySpriteController : MonoBehaviour
{
    [Tooltip("Перетащите сюда все 2D спрайты, цвет которых должен меняться")]
    public List<SpriteRenderer> tintedSprites;
    private PeriodSettings currentPeriodPlan;
    private PeriodSettings previousPeriodPlan;
    private float periodTimer;

    private readonly Dictionary<SpriteRenderer, float> _originalAlphas = new();

    private void Start()
    {
        if (ClientSpawner.Instance == null)
        {
            Debug.LogError("TimeOfDaySpriteController не может найти ClientSpawner!");
            enabled = false;
            return;
        }

        _originalAlphas.Clear();
        foreach (var sprite in tintedSprites)
        {
            if (sprite != null)
            {
                _originalAlphas[sprite] = sprite.color.a;
            }
        }
    }

    private void Update()
    {
        if (tintedSprites == null || tintedSprites.Count == 0 || ClientSpawner.Instance == null)
            return;
        
        currentPeriodPlan = ClientSpawner.Instance.GetCurrentPeriodPlan();
        previousPeriodPlan = ClientSpawner.Instance.GetPreviousPeriodPlan();
        periodTimer = ClientSpawner.Instance.GetPeriodTimer();

        if (currentPeriodPlan == null || previousPeriodPlan == null)
            return;

        var duration = currentPeriodPlan.durationInSeconds;
        if (duration <= 0)
            return;

        // Плавно вычисляем нужный цвет RGB (без альфы)
        float progress = Mathf.Clamp01(periodTimer / duration);
        // Получаем цвета из настроек периода
        Color prevColor = previousPeriodPlan.panelColor;
        Color currentColor = currentPeriodPlan.panelColor;

        // Интерполируем только RGB компоненты
        Color targetColorRGB = Color.Lerp(prevColor, currentColor, progress);

        // Применяем вычисленный цвет ко всем спрайтам в списке, сохраняя их исходную альфу
        foreach (var sprite in tintedSprites)
        {
            if (sprite != null)
            {
                // --- ИЗМЕНЕНИЕ НАЧАЛО: Устанавливаем цвет с сохраненной альфой ---
                float originalAlpha = _originalAlphas.ContainsKey(sprite) ? _originalAlphas[sprite] : 1f; // Берем сохраненную альфу или 1 по умолчанию
                sprite.color = new Color(targetColorRGB.r, targetColorRGB.g, targetColorRGB.b, originalAlpha); // Применяем RGB от Lerp и исходную Alpha
                // --- ИЗМЕНЕНИЕ КОНЕЦ ---
            }
        }
    }
}