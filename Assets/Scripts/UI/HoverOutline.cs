// Файл: HoverOutline.cs
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class HoverOutline : MonoBehaviour
{
    [Tooltip("Толщина обводки при наведении")]
    public float outlineWidth = 2f;

    private SpriteRenderer spriteRenderer;
    
    // Используем числовые ID для свойств шейдера, это работает быстрее
    private static readonly int OutlineWidthID = Shader.PropertyToID("_OutlineWidth");

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        // ВАЖНО: Создаем копию материала, чтобы не менять его для всех персонажей сразу
        spriteRenderer.material = new Material(spriteRenderer.material);
    }

    private void OnMouseEnter()
    {
        // Когда мышь наведена, устанавливаем толщину обводки
        spriteRenderer.material.SetFloat(OutlineWidthID, outlineWidth);
    }

    private void OnMouseExit()
    {
        // Когда мышь убрана, сбрасываем толщину на 0
        spriteRenderer.material.SetFloat(OutlineWidthID, 0);
    }
}