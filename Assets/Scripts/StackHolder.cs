// Файл: StackHolder.cs
using UnityEngine;
using System.Collections.Generic;

public class StackHolder : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Компонент SpriteRenderer, который будет отображать стопку.")]
    public SpriteRenderer stackSpriteRenderer;
    [Tooltip("Спрайты для разного размера стопки (маленькая, средняя, большая).")]
    public List<Sprite> stackSizeSprites;

    void Start()
    {
        // Прячем спрайт при старте
        if (stackSpriteRenderer != null)
        {
            stackSpriteRenderer.enabled = false;
        }
    }

    // Показать стопку в руках
    public void ShowStack(int documentCount, int maxStackSize)
    {
        if (stackSpriteRenderer == null || stackSizeSprites.Count == 0 || documentCount == 0) return;

        // Определяем, какой спрайт показать
        float ratio = (float)documentCount / maxStackSize;
        int spriteIndex = Mathf.FloorToInt(ratio * (stackSizeSprites.Count - 0.01f));
        spriteIndex = Mathf.Clamp(spriteIndex, 0, stackSizeSprites.Count - 1);

        stackSpriteRenderer.sprite = stackSizeSprites[spriteIndex];
        stackSpriteRenderer.enabled = true;
    }

    // Спрятать стопку
    public void HideStack()
    {
        if (stackSpriteRenderer != null)
        {
            stackSpriteRenderer.enabled = false;
        }
    }
}