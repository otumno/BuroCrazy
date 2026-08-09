using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems; // Нужно для интерфейсов
using UnityEngine.UI;
using Managers;         // Для AudioManager
using Scriptables.Audio; // Для SoundID

// Требуем Image, так как работаем в UI
[RequireComponent(typeof(Image))]
public class DeskInteractiveItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Визуал")]
    [Tooltip("Объект с обводкой (должен быть внутри, RaycastTarget = OFF)")]
    [SerializeField] private GameObject outlineObject;
    
    [Tooltip("Лампочка или уведомление")]
    [SerializeField] protected GameObject notificationOverlay;

    [Header("Настройки")]
    public bool isInteractable = true;

    [Header("Аудио")]
    [Tooltip("Звук при наведении (свист, шуршание)")]
    public SoundID hoverSound = SoundID.UI_Hover;
    [Tooltip("Звук при нажатии (клик, звонок, открытие)")]
    public SoundID clickSound = SoundID.UI_Click_Default;

    [Header("События")]
    public UnityEvent OnClick;

    protected virtual void Start()
    {
        if (outlineObject != null) outlineObject.SetActive(false);
        
        // Магия для игнорирования прозрачности (если есть Read/Write на текстуре)
        //var img = GetComponent<Image>();
        //if (img != null) img.alphaHitTestMinimumThreshold = 0.1f;

        CheckAvailability();
    }

    // --- ЛОГИКА НАВЕДЕНИЯ (Мышь вошла) ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isInteractable) return;

        if (outlineObject != null) outlineObject.SetActive(true);
        
        // Играем звук наведения
        if (AudioManager.Instance != null && hoverSound != SoundID.None)
        {
            AudioManager.Instance.PlaySound(hoverSound);
        }
    }

    // --- ЛОГИКА УХОДА (Мышь ушла) ---
    public void OnPointerExit(PointerEventData eventData)
    {
        if (outlineObject != null) outlineObject.SetActive(false);
    }

    // --- ЛОГИКА КЛИКА ---
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isInteractable) return;

        // Играем звук клика
        if (AudioManager.Instance != null && clickSound != SoundID.None)
        {
            AudioManager.Instance.PlaySound(clickSound);
        }

        // Вызываем событие (открытие панели и т.д.)
        OnClick?.Invoke();
    }

    public virtual void CheckAvailability() 
    { 
        // Базовая реализация пустая, переопределяется в наследниках (DeskCalculator)
    }
}