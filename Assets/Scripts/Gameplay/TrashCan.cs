using UnityEngine;
using System.Collections.Generic;

public class TrashCan : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Максимальное количество мусора, которое может вместить бак.")]
    public int capacity = 10;
    [Tooltip("Радиус, в котором клиенты будут 'целиться' в этот бак.")]
    public float attractionRadius = 5f;

    [Header("Визуализация")]
    [Tooltip("Список спрайтов для отображения разной степени наполненности.")]
    public List<Sprite> fillSprites;
    [Tooltip("Компонент SpriteRenderer, который будет отображать состояние.")]
    public SpriteRenderer spriteRenderer;

    // --- Текущее состояние ---
    public int CurrentFillCount { get; private set; } = 0;
    public bool IsFull => CurrentFillCount >= capacity;

    // Статический список для быстрого доступа ко всем бакам на сцене
    public static List<TrashCan> AllTrashCans = new List<TrashCan>();

    private void OnEnable()
    {
        if (!AllTrashCans.Contains(this))
        {
            AllTrashCans.Add(this);
        }
    }

    private void OnDisable()
    {
        if (AllTrashCans.Contains(this))
        {
            AllTrashCans.Remove(this);
        }
    }

    /// <summary>
    /// Добавляет одну единицу мусора в бак.
    /// </summary>
    public void AddTrash()
    {
        if (IsFull) return;
        CurrentFillCount++;
        UpdateVisuals();
    }

    /// <summary>
    /// "Опустошает" бак (вызывается Уборщиком).
    /// </summary>
    public void Empty()
    {
        CurrentFillCount = 0;
        UpdateVisuals();
    }

    /// <summary>
    /// Обновляет спрайт в зависимости от наполненности.
    /// </summary>
    private void UpdateVisuals()
    {
        if (spriteRenderer == null || fillSprites == null || fillSprites.Count == 0) return;

        // Вычисляем, какой спрайт показать
        float fillRatio = (float)CurrentFillCount / capacity;
        int spriteIndex = Mathf.FloorToInt(fillRatio * (fillSprites.Count - 0.01f));
        spriteIndex = Mathf.Clamp(spriteIndex, 0, fillSprites.Count - 1);

        spriteRenderer.sprite = fillSprites[spriteIndex];
    }
}