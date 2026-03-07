// Файл: Assets/Scripts/UI/SmartRoomLabel.cs
using UnityEngine;
using UnityEngine.EventSystems; // Обязательно для работы UI зон!
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
// Добавляем интерфейсы IPointerEnterHandler и IPointerExitHandler для отслеживания мыши над UI
public class SmartRoomLabel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Настройки")]
    public string roomTitle = "Комната";
    public float hideDelay = 0.5f;
    public float fadeSpeed = 10f;

    [Header("Ссылки")]
    public TextMeshProUGUI titleText;
    
    [Tooltip("Заполнять ТОЛЬКО если вы используете физические коллайдеры вместо UI зон")]
    public Collider2D roomCollider;

    private CanvasGroup _canvasGroup;
    private bool _isHoveredByUI;
    private bool _isHoveredByPhysics;
    private float _lastHoverTime = -100f; 
    private float _pingEndTime = -100f; 
    private Camera _mainCamera;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _mainCamera = Camera.main;

        if (titleText != null) titleText.text = roomTitle;
        
        _canvasGroup.alpha = 0f; // Принудительно скрываем на старте
    }

    void Update()
    {
        // 1. Проверяем Паузу (Только по TimeScale - это самый надежный способ)
        bool isPaused = Time.timeScale <= 0.01f;

        // 2. Проверяем физические коллайдеры (если они назначены)
        CheckPhysicsHover();

        // 3. Общее состояние наведения (либо UI зона, либо физика)
        bool currentlyHovered = _isHoveredByUI || _isHoveredByPhysics;

        if (currentlyHovered)
        {
            _lastHoverTime = Time.unscaledTime; // Обновляем таймер, пока мышь внутри зоны
        }

        // 4. Активен ли "Пинг" (изменение статуса)
        bool isPingActive = Time.unscaledTime < _pingEndTime;

        // 5. Итоговое решение: Видно, если Пауза ИЛИ Наведено ИЛИ Пинг ИЛИ не вышло время задержки
        bool shouldBeVisible = isPaused || currentlyHovered || (Time.unscaledTime < _lastHoverTime + hideDelay) || isPingActive;

        // Плавная анимация
        float targetAlpha = shouldBeVisible ? 1f : 0f;
        _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);
    }

    // --- СОБЫТИЯ UI (Для ваших прозрачных зон захвата мыши) ---
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHoveredByUI = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHoveredByUI = false;
    }
    // --------------------------------------------------------

    private void CheckPhysicsHover()
    {
        if (_mainCamera == null || roomCollider == null)
        {
            _isHoveredByPhysics = false;
            return;
        }

        Vector2 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        _isHoveredByPhysics = roomCollider.OverlapPoint(mousePos);
    }
    
    public void SetTitle(string title)
    {
        roomTitle = title;
        if (titleText != null) titleText.text = roomTitle;
    }

    /// <summary>
    /// Вызывается из других скриптов для привлечения внимания
    /// </summary>
    public void Ping(float duration = 2.5f)
    {
        _pingEndTime = Time.unscaledTime + duration;
    }
}
