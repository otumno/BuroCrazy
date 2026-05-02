using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Managers;

public class TimelineController : MonoBehaviour
{
    public static TimelineController Instance { get; private set; }

    [Header("UI Components")]
    public RectTransform currentTimeMarker;
    public RectTransform periodBgPrefab;
    public Transform periodsContainer;
    public RectTransform clientIconPrefab;
    public Transform iconsContainer;

    [Header("Settings")]
    public float barWidth = 800f; // резервное значение, если transform.rect.width не доступен
    [Range(0.0001f, 0.01f)]
    public float compressionFactor = 0.001f;
    public int maxIcons = 50;

    public float DayStartTime { get; private set; }
    private List<Image> periodBgs = new List<Image>();
    private List<RectTransform> clientIcons = new List<RectTransform>();
    private Stack<RectTransform> iconPool = new Stack<RectTransform>();

    private PeriodInfo[] periods;
    private float[] spawnTimes;
    private float containerWidth;

    public void Show()
    {
        gameObject.SetActive(true);
        // Гарантированное перестроение при каждом показе
        RebuildTimeline();
    }
    public void Hide() => gameObject.SetActive(false);

    [System.Serializable]
    public class PeriodInfo
    {
        public float startTime;
        public float duration;
        public Color color;
    }

    private void Awake()
    {
        Instance = this;

        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged += OnDayChanged;
        else
            Debug.LogWarning("[TimelineController] TimeManager.Instance == null в Awake");

        if (WaveManager.Instance != null)
            WaveManager.Instance.OnSpawnPlanUpdated += RebuildTimeline;
    }

    private void Start()
    {
        Hide();
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnDayChanged -= OnDayChanged;

        if (WaveManager.Instance != null)
            WaveManager.Instance.OnSpawnPlanUpdated -= RebuildTimeline;
    }

    private void OnDayChanged(int newDay)
    {
        RebuildTimeline();
    }

    [ContextMenu("Force Rebuild")]
    public void ForceRebuild()
    {
        RebuildTimeline();
    }

    // Инициализация происходит при первом Show(), если периоды ещё не заданы

    private void ClearTimeline()
    {
        foreach (Transform t in periodsContainer) Destroy(t.gameObject);
        foreach (Transform t in iconsContainer) Destroy(t.gameObject);
        periodBgs.Clear();
        clientIcons.Clear();
        iconPool.Clear();
    }

    private void RebuildTimeline()
    {
        if (TimeManager.Instance == null || TimeManager.Instance.mainCalendarDay == null)
        {
            Debug.LogWarning("[Timeline] TimeManager или календарь не готовы");
            return;
        }

        var day = TimeManager.Instance.mainCalendarDay;
        int periodCount = day.periodSettings != null ? day.periodSettings.Count : 0;

        periods = new PeriodInfo[periodCount];
        for (int i = 0; i < periodCount; i++)
        {
            var ps = day.periodSettings[i];
            float start = 0;
            for (int j = 0; j < i; j++)
            {
                start += day.periodSettings[j].durationInSeconds;
            }
            periods[i] = new PeriodInfo
            {
                startTime = start,
                duration = ps.durationInSeconds,
                color = ps.panelColor
            };
        }

        // Используем готовое расписание из WaveManager
        float[] spawnTimesArray = new float[0];
        if (WaveManager.Instance?.SpawnSchedule != null && WaveManager.Instance.SpawnSchedule.Count > 0)
        {
            spawnTimesArray = WaveManager.Instance.SpawnSchedule.ToArray();
        }
        else if (WaveManager.Instance?.dailySpawnPlan != null && WaveManager.Instance.dailySpawnPlan.Count > 0)
        {
            // Fallback - вычисляем вручную если SpawnSchedule не доступен
            var plan = WaveManager.Instance.dailySpawnPlan;
            List<float> times = new List<float>();
            foreach (var kvp in plan)
            {
                int clientCount = kvp.Value.clientCount;
                if (clientCount > 0)
                {
                    PeriodInfo periodInfo = null;
                    for (int i = 0; i < periods.Length; i++)
                    {
                        if (i < day.periodSettings.Count && day.periodSettings[i].PeriodType == kvp.Key)
                        {
                            periodInfo = periods[i];
                            break;
                        }
                    }
                    if (periodInfo == null) continue;

                    float start = periodInfo.startTime;
                    float duration = periodInfo.duration;
                    for (int i = 0; i < clientCount; i++)
                    {
                        float t = start + (duration * i / clientCount);
                        times.Add(t);
                    }
                }
            }
            spawnTimesArray = times.ToArray();
        }

        // Всегда очищаем перед перестройкой
        ClearTimeline();

        // Логируем информацию о запланированных клиентах
        int totalClientsInPlan = 0;
        if (WaveManager.Instance?.dailySpawnPlan != null)
        {
            foreach (var kvp in WaveManager.Instance.dailySpawnPlan)
            {
                totalClientsInPlan += kvp.Value.clientCount;
            }
        }
        Debug.Log($"[Timeline] Инициализация: {periods.Length} периодов, {spawnTimesArray.Length} иконок (всего клиентов в плане: {totalClientsInPlan})");
        Initialize(periods, spawnTimesArray);
    }

    public void Initialize(PeriodInfo[] periodsArray, float[] spawnTimesArray)
    {
        periods = periodsArray;
        spawnTimes = spawnTimesArray ?? new float[0];

        // Получаем время начала дня
        if (TimeManager.Instance != null)
            DayStartTime = TimeManager.Instance.GetCurrentDayStartTime();
        else
            DayStartTime = Time.time;

        // Получаем фактическую ширину контейнера
        containerWidth = ((RectTransform)transform).rect.width;
        if (containerWidth <= 0) containerWidth = barWidth;

        Debug.Log($"[Timeline] Container width: {containerWidth}, compression: {compressionFactor}");


        // Создаём фоны периодов
        for (int i = 0; i < periods.Length; i++)
        {
            var bg = Object.Instantiate(periodBgPrefab, periodsContainer);
            var img = bg.GetComponent<Image>();
            Color c = periods[i].color;
            c.a = 1f;
            img.color = c;

            // Настройка RectTransform: якорь и пивот слева
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(100, rt.sizeDelta.y); // начальная ширина

            bg.gameObject.SetActive(true);
            periodBgs.Add(img);
        }

        // Устранение зазоров: пробегаем по фонам и принудительно стыкуем
        for (int i = 1; i < periodBgs.Count; i++)
        {
            var prevRt = periodBgs[i - 1].rectTransform;
            var currRt = periodBgs[i].rectTransform;
            float prevRight = prevRt.anchoredPosition.x + prevRt.sizeDelta.x;
            currRt.anchoredPosition = new Vector2(prevRight, 0);
        }

        // Пул иконок клиентов
        for (int i = 0; i < maxIcons; i++)
        {
            var icon = Object.Instantiate(clientIconPrefab, iconsContainer);
            var iconImg = icon.GetComponent<Image>();
            iconImg.raycastTarget = false;

            // Настройка RectTransform иконки: якорь и пивот слева
            icon.anchorMin = new Vector2(0, 0.5f);
            icon.anchorMax = new Vector2(0, 0.5f);
            icon.pivot = new Vector2(0, 0.5f);
            icon.anchoredPosition = new Vector2(0, icon.anchoredPosition.y);

            icon.gameObject.SetActive(false);
            iconPool.Push(icon);
        }

        // Активируем иконки под времена спавна
        if (spawnTimes != null && spawnTimes.Length > 0)
        {
            int iconCount = Mathf.Min(spawnTimes.Length, maxIcons);
            for (int i = 0; i < iconCount; i++)
            {
                var icon = iconPool.Pop();
                icon.gameObject.SetActive(true);
                clientIcons.Add(icon);
            }
        }
    }

    private void Update()
    {
        if (periods == null || containerWidth <= 0) return;

        float currentTime = 0f;
        if (TimeManager.Instance != null)
            currentTime = TimeManager.Instance.GetCurrentTimeSinceDayStart();
        else if (DayStartTime > 0)
            currentTime = Time.time - DayStartTime;

        // Обновление фонов периодов
        float prevXRight = 0f;
        for (int i = 0; i < periods.Length; i++)
        {
            var period = periods[i];
            float remainingStart = period.startTime - currentTime;
            float remainingEnd = period.startTime + period.duration - currentTime;

            float xLeft = containerWidth * (1f - Mathf.Exp(-compressionFactor * Mathf.Max(0, remainingStart)));
            float xRight = containerWidth * (1f - Mathf.Exp(-compressionFactor * Mathf.Max(0, remainingEnd)));

            // Стыковка: xLeft текущего = xRight предыдущего (устраняем зазоры)
            if (i > 0)
            {
                xLeft = prevXRight;
            }

            float width = xRight - xLeft;

            if (width < 0.5f)
            {
                periodBgs[i].gameObject.SetActive(false);
            }
            else
            {
                periodBgs[i].gameObject.SetActive(true);
                var rt = periodBgs[i].rectTransform;
                rt.anchoredPosition = new Vector2(xLeft, 0);
                rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
            }

            prevXRight = xRight;
        }

        // Обновление иконок клиентов
        for (int i = 0; i < clientIcons.Count; i++)
        {
            if (i >= spawnTimes.Length) break;

            float remaining = spawnTimes[i] - currentTime;
            if (remaining <= 0)
            {
                clientIcons[i].gameObject.SetActive(false);
            }
            else
            {
                clientIcons[i].gameObject.SetActive(true);
                float x = containerWidth * (1f - Mathf.Exp(-compressionFactor * remaining));
                clientIcons[i].anchoredPosition = new Vector2(x, clientIcons[i].anchoredPosition.y);
            }
        }
    }
}