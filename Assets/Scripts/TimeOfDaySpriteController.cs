// Файл: Assets/Scripts/TimeOfDaySpriteController.cs
using UnityEngine;
using System.Collections.Generic;

public class TimeOfDaySpriteController : MonoBehaviour
{
    [Tooltip("Перетащите сюда все 2D спрайты, цвет которых должен меняться")]
    public List<SpriteRenderer> tintedSprites;
    private PeriodSettings currentPeriodPlan;
    private PeriodSettings previousPeriodPlan;
    private float periodTimer;

    // --- ИЗМЕНЕНИЕ НАЧАЛО: Список для хранения исходной альфы ---
    private Dictionary<SpriteRenderer, float> originalAlphas = new Dictionary<SpriteRenderer, float>();
    // --- ИЗМЕНЕНИЕ КОНЕЦ ---


    void Start()
    {
        if (ClientSpawner.Instance == null)
        {
            Debug.LogError("TimeOfDaySpriteController не может найти ClientSpawner!");
            enabled = false;
            return; // Добавлено
        }

        // --- ИЗМЕНЕНИЕ НАЧАЛО: Сохраняем исходную альфу каждого спрайта ---
        originalAlphas.Clear();
        foreach (var sprite in tintedSprites)
        {
            if (sprite != null)
            {
                originalAlphas[sprite] = sprite.color.a; // Запоминаем текущую альфу
            }
        }
        // --- ИЗМЕНЕНИЕ КОНЕЦ ---
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
                float originalAlpha = originalAlphas.ContainsKey(sprite) ? originalAlphas[sprite] : 1f; // Берем сохраненную альфу или 1 по умолчанию
                sprite.color = new Color(targetColorRGB.r, targetColorRGB.g, targetColorRGB.b, originalAlpha); // Применяем RGB от Lerp и исходную Alpha
                // --- ИЗМЕНЕНИЕ КОНЕЦ ---
            }
        }
    }
}