using UnityEngine;
using TMPro;
using Managers; // Для доступа к TimeManager или проверки паузы

[RequireComponent(typeof(CanvasGroup))]
public class SmartRoomLabel : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Статичное название комнаты (например, 'Касса')")]
    public string roomTitle = "Комната";
    [Tooltip("Задержка перед исчезновением (сек)")]
    public float hideDelay = 0.5f;
    [Tooltip("Скорость появления/исчезновения")]
    public float fadeSpeed = 10f;

    [Header("Ссылки")]
    [Tooltip("Текстовое поле для заголовка (статичное)")]
    public TextMeshProUGUI titleText;
    
    // Ссылки на зоны отслеживания мыши (коллайдеры комнаты)
    // Если UI висит на том же объекте, что и коллайдер, это поле можно не заполнять вручную
    [Tooltip("Коллайдер комнаты. Если пусто, ищет на этом же объекте или в родителях.")]
    public Collider2D roomCollider;

    private CanvasGroup _canvasGroup;
    private bool _isHovered;
    private float _lastHoverTime = -100f; // Инициализируем в прошлое чтобы в начале все было скрыто
    private Camera _mainCamera;

    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _mainCamera = Camera.main;

        // Автопоиск коллайдера, если не назначен
        if (roomCollider == null)
        {
            roomCollider = GetComponentInParent<Collider2D>();
            if (roomCollider == null)
            {
                Debug.LogWarning($"[SmartRoomLabel] Коллайдер не найден для комнаты '{roomTitle}' на объекте {gameObject.name}");
            }
        }

        // Установка заголовка
        if (titleText != null)
        {
            titleText.text = roomTitle;
        }
        
        // Скрываем при старте
        _canvasGroup.alpha = 0f;
    }

    void Update()
    {
        // 1. Проверяем Паузу (Time.timeScale или MainUIManager)
        bool isPausedByTimeScale = Time.timeScale == 0f;
        bool isPausedByManager = MainUIManager.Instance != null && MainUIManager.Instance.pauseCount > 0;
        bool isPaused = isPausedByTimeScale || isPausedByManager;

        // 2. Проверяем Мышь (Рейкаст в коллайдер комнаты)
        CheckMouseHover();

        // 3. Определяем, должны ли мы быть видны
        // Видны, если: Пауза ИЛИ Мышь наведена ИЛИ прошло мало времени с момента ухода мыши
        bool shouldBeVisible = isPaused || _isHovered || (Time.time < _lastHoverTime + hideDelay);

        // 4. Плавная анимация прозрачности
        float targetAlpha = shouldBeVisible ? 1f : 0f;
        
        // Используем unscaledDeltaTime, чтобы анимация работала даже на паузе!
        _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * fadeSpeed);
    }

    private void CheckMouseHover()
    {
        if (_mainCamera == null || roomCollider == null)
        {
            _isHovered = false;
            return;
        }

        Vector2 mousePos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
        
        // Самая простая проверка: попадает ли точка мыши в коллайдер
        bool hit = roomCollider.OverlapPoint(mousePos);

        if (hit)
        {
            if (!_isHovered)
            {
                _isHovered = true;
                _lastHoverTime = Time.time; // Обновляем таймер только при входе
            }
        }
        else
        {
            _isHovered = false;
        }
    }
    
    // Метод для настройки из инспектора (если нужно менять название программно)
    public void SetTitle(string title)
    {
        roomTitle = title;
        if (titleText != null) titleText.text = roomTitle;
    }
}