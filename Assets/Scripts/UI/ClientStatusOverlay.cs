using UnityEngine;
using UnityEngine.UI;
using Managers;

public class ClientStatusOverlay : MonoBehaviour
{
    [Header("DEBUG")]
    public bool debugForceVisible = false; // <--- НАЖМИ ЭТУ ГАЛОЧКУ В ИГРЕ

    [Header("Главный контейнер")]
    public GameObject overlayRoot;
    
    [Header("Терпение")]
    public Slider patienceSlider;
    public Image patienceFillImage;
    public Gradient patienceGradient;

    [Header("Прогресс")]
    public Slider progressSlider;
    public GameObject moneyIcon;

    private ClientPathfinding _client;
    private Canvas _canvas;
    private bool _isInitialized = false;

    void Awake()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    public void Initialize(ClientPathfinding client)
    {
        _client = client;
        _canvas = GetComponent<Canvas>();
        
        if (_canvas != null)
        {
            _canvas.worldCamera = Camera.main;
            
            // --- ВАЖНОЕ ИЗМЕНЕНИЕ: Просто ставим огромный Order на дефолтном слое ---
            // Это работает надежнее, если слоя "UI" не существует
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 32000; 
            // ------------------------------------------------------------------------
        }

        _isInitialized = true;
    }

    void LateUpdate()
    {
        if (!_isInitialized || _client == null || overlayRoot == null) return;

        // 1. Проверяем Паузу
        bool isPaused = Mathf.Approximately(Time.timeScale, 0f);

        // 2. Логика видимости: Пауза ИЛИ Ручной Дебаг
        bool shouldBeVisible = isPaused || debugForceVisible;

        // Включаем/Выключаем объект
        if (overlayRoot.activeSelf != shouldBeVisible)
        {
            overlayRoot.SetActive(shouldBeVisible);
            Debug.Log($"[Overlay] {name}: Переключение видимости на {shouldBeVisible}. (Pause={isPaused}, Force={debugForceVisible})");
        }

        // 3. Обновляем данные, если видно
        if (shouldBeVisible)
        {
            if (transform.rotation != Quaternion.identity) transform.rotation = Quaternion.identity;
            UpdatePatience();
            UpdateProgress();
        }
    }

    private void UpdatePatience()
    {
        if (patienceSlider == null) return;

        float heat = _client.PatienceHeat; // Получаем 0..1

        patienceSlider.value = heat; // Заполняем слева направо

        if (patienceFillImage != null)
        {
            // 0 (начало) = Зеленый, 1 (конец) = Красный
            // Убедись, что градиент в инспекторе настроен: Left=Green, Right=Red
            patienceFillImage.color = patienceGradient.Evaluate(heat); 
        }
    }

    private void UpdateProgress()
    {
        if (progressSlider == null) return;
        float progress = (_client.stateMachine != null) ? _client.stateMachine.GetNormalizedProgress() : 0f;
        progressSlider.value = progress;
        if (moneyIcon != null)
        {
            bool showMoney = progress >= 0.99f && _client.billToPay > 0;
            if (moneyIcon.activeSelf != showMoney) moneyIcon.SetActive(showMoney);
        }
    }
}