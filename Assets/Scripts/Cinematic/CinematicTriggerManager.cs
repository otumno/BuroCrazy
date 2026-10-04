// === FILE: Assets/Scripts/Cinematic/CinematicTriggerManager.cs ===
using System;
using System.Collections.Generic;
using UnityEngine;
using Managers;
using Data.Calendar;

namespace CinematicSystem
{
    /// <summary>
    /// Данные одного триггера.
    /// </summary>
    [Serializable]
    public class CinematicTriggerData
    {
        [Tooltip("Уникальный идентификатор триггера")]
        public string id;
        
        [Tooltip("Включить триггер")]
        public bool enabled = true;
        
        [Tooltip("Тип триггера")]
        public TriggerType triggerType;
        
        [Tooltip("Необходимый день для срабатывания (0 = любой день)")]
        public int requiredDay = 0;
        
        [Tooltip("Граф для воспроизведения")]
        public CinematicGraph graphToPlay;
        
        [Tooltip("Запустить только один раз")]
        public bool once = true;
        
        [Tooltip("Повторять каждый раз при выполнении условий")]
        public bool repeatOnCondition = false;
        
        [Tooltip("Задержка перед запуском (секунды)")]
        public float delay = 0f;
        
        [Tooltip("Использовать случайную задержку")]
        public bool useRandomDelay = false;
        
        [Tooltip("Минимальная случайная задержка (секунды)")]
        public float minDelay = 0f;
        
        [Tooltip("Максимальная случайная задержка (секунды)")]
        public float maxDelay = 0f;
        
        [Tooltip("Не запускать, если до конца периода меньше этого значения (секунды)")]
        public float excludeEndSeconds = 0f;
        
        [Tooltip("Режим выполнения графа")]
        public ExecutionMode executionMode = ExecutionMode.FullControl;
        
        [Tooltip("Принудительный фоновый режим")]
        public bool forceBackground = false;
        
        [Tooltip("Дополнительные параметры для графа")]
        public Dictionary<string, object> runtimeParameters;
        
        [Tooltip("Уже сработал (внутреннее поле)")]
        public bool hasTriggered = false;

        // Граф доигран до конца. Именно это сохраняется в сейв, а не hasTriggered: если выйти посреди
        // катсцены, событие при загрузке должно повториться.
        [NonSerialized]
        public bool hasCompleted = false;
        
        /// <summary>
        /// Проверить, выполняются ли условия триггера.
        /// </summary>
        public bool CheckConditions()
        {
            // Проверка дня
            if (requiredDay > 0 && TimeManager.Instance != null)
            {
                if (TimeManager.Instance.GetCurrentDay() != requiredDay)
                    return false;
            }
            
            return true;
        }
    }

    /// <summary>
    /// Единый менеджер триггеров кинематиков.
    /// Управляет всеми триггерами в одном месте.
    /// </summary>
    public class CinematicTriggerManager : MonoBehaviour
    {
        public static CinematicTriggerManager Instance { get; private set; }

        [Header("Настройки")]
        [Tooltip("Список всех триггеров")]
        public List<CinematicTriggerData> triggers = new List<CinematicTriggerData>();
        
        [Tooltip("Автоматически регистрировать триггеры при старте")]
        public bool autoRegisterOnStart = true;
        
        [Tooltip("Отладочные сообщения")]
        public bool debugLog = true;

        /// <summary>
        /// Граф триггера доигран до конца (аргумент — id триггера). Через него игровая логика реагирует
        /// на окончание катсцены (например, EndingManager на визит Инспектора), не запуская граф сама.
        /// </summary>
        public event Action<string> OnTriggerCompleted;

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogWarning("[CinematicTriggerManager] Duplicate instance detected, destroying");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (autoRegisterOnStart)
                SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Подписаться на все системные события.
        /// </summary>
        private void SubscribeToEvents()
        {
            // Подписка на TimeManager
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayChanged += OnDayChanged;
                TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;
            }
            
            Debug.Log("[CinematicTriggerManager] Подписка на события выполнена");
        }

        /// <summary>
        /// Отписаться от всех событий.
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayChanged -= OnDayChanged;
                TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
            }
        }

        // === Обработчики событий ===

        private void OnDayChanged(int day)
        {
            if (debugLog) Debug.Log($"[CinematicTriggerManager] День изменился: {day}");
            TriggerDayStart(day);
        }

        /// <summary>
        /// Запускает события начала дня (OnDayStart). Вызывается при смене дня, а также MainUIManager'ом
        /// после загрузки: сессия всегда начинается с начала дня, но TimeManager.OnDayChanged при этом не приходит.
        /// </summary>
        /// <returns>true, если сразу (без задержки) запущена катсцена с полным контролем.</returns>
        public bool TriggerDayStart(int day)
        {
            bool isFullControlStarted = false;

            foreach (var trigger in triggers)
            {
                if (!trigger.enabled) continue;
                if (trigger.triggerType != TriggerType.OnDayStart) continue;
                if (trigger.hasTriggered && trigger.once && !trigger.repeatOnCondition) continue;

                if (trigger.requiredDay <= 0 || trigger.requiredDay == day)
                {
                    if (trigger.CheckConditions() && TryTrigger(trigger))
                    {
                        isFullControlStarted |= !trigger.forceBackground && trigger.executionMode == ExecutionMode.FullControl;
                    }
                }
            }

            return isFullControlStarted;
        }

        private void OnPeriodChanged(PeriodSettings period)
        {
            if (debugLog) Debug.Log($"[CinematicTriggerManager] Период изменился: {period.PeriodType}");
            
            foreach (var trigger in triggers)
            {
                if (!trigger.enabled) continue;
                if (trigger.triggerType != TriggerType.OnPeriodStart) continue;
                if (trigger.hasTriggered && trigger.once && !trigger.repeatOnCondition) continue;
                
                // Проверяем excludeEndSeconds
                if (trigger.excludeEndSeconds > 0 && TimeManager.Instance != null)
                {
                    float currentTimeInPeriod = TimeManager.Instance.GetPeriodTimer();
                    float remainingTime = period.durationInSeconds - currentTimeInPeriod;
                    if (remainingTime < trigger.excludeEndSeconds)
                    {
                        if (debugLog) Debug.Log($"[CinematicTriggerManager] Пропуск триггера {trigger.id}: до конца периода {remainingTime:F1}с < {trigger.excludeEndSeconds}с");
                        continue;
                    }
                }
                
                if (trigger.CheckConditions())
                {
                    TryTrigger(trigger, period.durationInSeconds);
                }
            }
        }

        // === Публичные методы управления триггерами ===

        /// <summary>
        /// Зарегистрировать новый триггер.
        /// </summary>
        public void RegisterTrigger(CinematicTriggerData data)
        {
            if (triggers.Exists(t => t.id == data.id))
            {
                Debug.LogWarning($"[CinematicTriggerManager] Триггер с id '{data.id}' уже существует");
                return;
            }
            triggers.Add(data);
            if (debugLog) Debug.Log($"[CinematicTriggerManager] Зарегистрирован триггер: {data.id}");
        }

        /// <summary>
        /// Удалить триггер по ID.
        /// </summary>
        public void UnregisterTrigger(string id)
        {
            triggers.RemoveAll(t => t.id == id);
            if (debugLog) Debug.Log($"[CinematicTriggerManager] Удалён триггер: {id}");
        }

        /// <summary>
        /// Включить/выключить триггер.
        /// </summary>
        public void EnableTrigger(string id, bool enable)
        {
            var trigger = triggers.Find(t => t.id == id);
            if (trigger != null)
            {
                trigger.enabled = enable;
                if (debugLog) Debug.Log($"[CinematicTriggerManager] Триггер {id} {(enable ? "включён" : "выключен")}");
            }
        }

        /// <summary>
        /// Сбросить состояние триггера (чтобы сработал снова).
        /// </summary>
        public void ResetTrigger(string id)
        {
            var trigger = triggers.Find(t => t.id == id);
            if (trigger != null)
            {
                trigger.hasTriggered = false;
                trigger.hasCompleted = false;
                if (debugLog) Debug.Log($"[CinematicTriggerManager] Триггер {id} сброшен");
            }
        }

        /// <summary>
        /// Сбросить все триггеры.
        /// </summary>
        public void ResetAllTriggers()
        {
            foreach (var trigger in triggers)
            {
                trigger.hasTriggered = false;
                trigger.hasCompleted = false;
            }
            if (debugLog) Debug.Log("[CinematicTriggerManager] Все триггеры сброшены");
        }

        /// <summary>
        /// Запустить триггер вручную по ID.
        /// </summary>
        public void TriggerNow(string id)
        {
            var trigger = triggers.Find(t => t.id == id);
            if (trigger != null)
            {
                TryTrigger(trigger);
            }
            else
            {
                Debug.LogWarning($"[CinematicTriggerManager] Триггер '{id}' не найден");
            }
        }

        /// <summary>
        /// Получить триггер по ID.
        /// </summary>
        public CinematicTriggerData GetTrigger(string id)
        {
            return triggers.Find(t => t.id == id);
        }

        /// <summary>
        /// Добавить триггер программно (упрощённый метод).
        /// </summary>
        public CinematicTriggerData AddTrigger(string id, TriggerType type, CinematicGraph graph)
        {
            var data = new CinematicTriggerData
            {
                id = id,
                triggerType = type,
                graphToPlay = graph
            };
            RegisterTrigger(data);
            return data;
        }

        // === Приватные методы ===

        /// <summary>
        /// Попытка запустить триггер с задержкой.
        /// </summary>
        /// <returns>true, если граф запущен сразу (без задержки).</returns>
        private bool TryTrigger(CinematicTriggerData trigger, float periodDuration = 0f)
        {
            if (trigger.graphToPlay == null)
            {
                Debug.LogWarning($"[CinematicTriggerManager] Граф для триггера '{trigger.id}' не назначен");
                return false;
            }

            // Вычисляем задержку
            float calculatedDelay = trigger.delay;
            
            if (trigger.useRandomDelay)
            {
                float availableWindow = periodDuration > 0 ? periodDuration - trigger.excludeEndSeconds : float.MaxValue;
                float effectiveMaxDelay = Mathf.Min(trigger.maxDelay, availableWindow - 0.1f);
                
                if (trigger.minDelay < effectiveMaxDelay)
                {
                    calculatedDelay = UnityEngine.Random.Range(trigger.minDelay, effectiveMaxDelay);
                    if (debugLog) Debug.Log($"[CinematicTriggerManager] Случайная задержка для {trigger.id}: {calculatedDelay:F1}с");
                }
                else
                {
                    calculatedDelay = trigger.minDelay;
                }
            }

            // Запускаем с задержкой или сразу
            if (calculatedDelay > 0)
            {
                StartCoroutine(DelayedTriggerCoroutine(trigger, calculatedDelay));
                return false;
            }

            return ExecuteTrigger(trigger);
        }

        private System.Collections.IEnumerator DelayedTriggerCoroutine(CinematicTriggerData trigger, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            ExecuteTrigger(trigger);
        }

        /// <summary>
        /// Выполнить триггер.
        /// </summary>
        private bool ExecuteTrigger(CinematicTriggerData trigger)
        {
            if (trigger.hasTriggered && trigger.once && !trigger.repeatOnCondition)
            {
                if (debugLog) Debug.Log($"[CinematicTriggerManager] Триггер '{trigger.id}' уже сработал и неповторяемый");
                return false;
            }

            if (debugLog) Debug.Log($"[CinematicTriggerManager] Запуск триггера: {trigger.id}");
            
            trigger.hasTriggered = true;

            // Находим или создаём CinematicPlayer
            CinematicPlayer player = FindObjectOfType<CinematicPlayer>();
            if (player == null)
            {
                var go = new GameObject("CinematicPlayer");
                player = go.AddComponent<CinematicPlayer>();
            }

            // Копируем параметры
            if (trigger.runtimeParameters != null && trigger.runtimeParameters.Count > 0)
            {
                trigger.graphToPlay.runtimeParameters = new Dictionary<string, object>(trigger.runtimeParameters);
            }

            // Определяем режим выполнения
            var mode = trigger.forceBackground ? ExecutionMode.Background : trigger.executionMode;

            Action onFinished = null;
            onFinished = () =>
            {
                player.OnFinished -= onFinished;
                // Play другого графа прерывает текущий через Stop() без OnFinished — тогда событие не доиграно.
                if (player.CurrentGraph != trigger.graphToPlay) return;

                trigger.hasCompleted = true;
                OnTriggerCompleted?.Invoke(trigger.id);
            };
            player.OnFinished += onFinished;

            player.Play(trigger.graphToPlay, mode);
            return true;
        }

        // === JSON сохранение/загрузка состояния ===

        /// <summary>
        /// Получить состояние всех триггеров для сохранения: какие события доиграны до конца.
        /// </summary>
        public List<SerializableTriggerState> GetTriggerStates()
        {
            var states = new List<SerializableTriggerState>();
            foreach (var trigger in triggers)
            {
                if (string.IsNullOrEmpty(trigger.id)) continue;

                states.Add(new SerializableTriggerState
                {
                    id = trigger.id,
                    hasCompleted = trigger.hasCompleted
                });
            }
            return states;
        }

        /// <summary>
        /// Восстановить состояние триггеров из сохранения. Триггеры, которых нет в сейве, считаются несработавшими:
        /// загрузку вызывают и поверх идущей сцены (перезагрузка после отстранения).
        /// </summary>
        public void RestoreTriggerStates(List<SerializableTriggerState> states)
        {
            foreach (var trigger in triggers)
            {
                var state = states?.Find(s => s.id == trigger.id);
                bool isCompleted = state != null && state.hasCompleted;
                trigger.hasCompleted = isCompleted;
                trigger.hasTriggered = isCompleted;
            }
        }
    }

    /// <summary>
    /// Сериализуемое состояние триггера для сохранения.
    /// </summary>
    [Serializable]
    public class SerializableTriggerState
    {
        public string id;
        public bool hasCompleted;
    }
}