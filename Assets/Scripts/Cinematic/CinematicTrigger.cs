// === FILE: Assets/Scripts/Cinematic/CinematicTrigger.cs ===
using System.Collections;
using UnityEngine;
using Managers;
using Data.Calendar;

namespace CinematicSystem
{
    /// <summary>
    /// Тип триггера для запуска кинематического графа.
    /// </summary>
    public enum TriggerType
    {
        OnDayStart,
        OnPeriodStart,
        OnFlagSet,
        OnMoneyReached,
        OnStrikeCount,
        OnEnterZone,
        OnDialogueEnd,
        OnUIClick,
        Manual
    }

    /// <summary>
    /// Компонент-триггер для запуска кинематического графа.
    /// </summary>
    public class CinematicTrigger : MonoBehaviour
    {
        [Header("Trigger Settings")]
        public TriggerType triggerType;
        
        [Tooltip("Необходимый день")]
        public int requiredDay = 1;
        
        [Tooltip("Необходимый период дня")]
        public CalendarDayPeriodType requiredPeriod;
        
        [Tooltip("Ключ требуемого флага")]
        public string requiredFlagKey;
        
        [Tooltip("Требуемое значение флага")]
        public int requiredFlagValue = 1;
        
        [Tooltip("Требуемая сумма денег")]
        public int requiredMoney = 0;
        
        [Tooltip("Требуемое количество страйков")]
        public int requiredStrikes = 0;
        
        [Header("Random Delay")]
        [Tooltip("Использовать случайную задержку")]
        public bool useRandomDelay = false;
        
        [Tooltip("Минимальная задержка (секунды)")]
        public float minDelay = 0f;
        
        [Tooltip("Максимальная задержка (секунды)")]
        public float maxDelay = 0f;
        
        [Tooltip("Не запускать, если до конца периода меньше этого значения (секунды)")]
        public float excludeEndSeconds = 0f;
        
        [Header("Graph Settings")]
        [Tooltip("Граф для воспроизведения")]
        public CinematicGraph graphToPlay;
        
        [Tooltip("Запустить только один раз")]
        public bool once = true;
        
        [Tooltip("Задержка перед запуском (фиксированная, если useRandomDelay=false)")]
        public float delay = 0f;
        
        [Tooltip("Режим выполнения графа")]
        public ExecutionMode executionMode = ExecutionMode.FullControl;
        
        [Tooltip("Принудительный фоновый режим")]
        public bool forceBackground = false;

        private bool triggered = false;

        private void Start()
        {
            Subscribe();
        }

        private void Subscribe()
        {
            switch (triggerType)
            {
                case TriggerType.OnDayStart:
                    if (TimeManager.Instance != null)
                        TimeManager.Instance.OnDayChanged += OnDayChanged;
                    break;
                    
                case TriggerType.OnPeriodStart:
                    if (TimeManager.Instance != null)
                        TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;
                    break;
                    
                case TriggerType.OnFlagSet:
                    // Подписка через StoryStateManager если есть событие
                    break;
                    
                case TriggerType.OnMoneyReached:
                    // Подписка через PlayerWallet если есть событие
                    break;
            }
        }

        private void OnDayChanged(int day)
        {
            if (triggered && once) return;
            if (day == requiredDay)
                TriggerWithDelay();
        }

        private void OnPeriodChanged(PeriodSettings period)
        {
            if (triggered && once) return;
            if ((period.PeriodType & requiredPeriod) != 0)
            {
                // Проверяем, что до конца периода足够 времени для запуска
                if (excludeEndSeconds > 0 && TimeManager.Instance != null)
                {
                    float currentTimeInPeriod = TimeManager.Instance.GetPeriodTimer();
                    float remainingTime = period.durationInSeconds - currentTimeInPeriod;
                    if (remainingTime < excludeEndSeconds)
                    {
                        Debug.Log($"[CinematicTrigger] Пропуск триггера: до конца периода {remainingTime:F1}с < {excludeEndSeconds}с");
                        return;
                    }
                }
                TriggerWithDelay(period.durationInSeconds);
            }
        }

        /// <summary>
        /// Запустить триггер с задержкой.
        /// </summary>
        public void Trigger()
        {
            TriggerWithDelay(0);
        }
        
        /// <summary>
        /// Запустить триггер с параметрами.
        /// </summary>
        public void PlayWithParameters(CinematicGraph graph, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            if (graph == null) return;
            
            // Копируем параметры в граф
            graph.runtimeParameters = new System.Collections.Generic.Dictionary<string, object>(parameters);
            
            var cinematicPlayer = FindObjectOfType<CinematicPlayer>();
            if (cinematicPlayer == null)
            {
                var go = new GameObject("CinematicPlayer");
                cinematicPlayer = go.AddComponent<CinematicPlayer>();
            }
            
            var mode = forceBackground ? ExecutionMode.Background : executionMode;
            cinematicPlayer.Play(graph, mode);
        }

        /// <summary>
        /// Сбросить состояние триггера (чтобы сработал снова).
        /// </summary>
        public void ResetTrigger()
        {
            triggered = false;
        }

        private void TriggerWithDelay(float periodDuration = 0)
        {
            if (triggered && once) return;
            triggered = true;
            
            // Вычисляем задержку
            float calculatedDelay = delay;
            
            if (useRandomDelay)
            {
                // Вычисляем доступное окно для запуска
                float availableWindow = periodDuration > 0 ? periodDuration - excludeEndSeconds : float.MaxValue;
                
                // Ограничиваем maxDelay доступным окном
                float effectiveMaxDelay = Mathf.Min(maxDelay, availableWindow - 0.1f);
                
                if (minDelay < effectiveMaxDelay)
                {
                    calculatedDelay = UnityEngine.Random.Range(minDelay, effectiveMaxDelay);
                    Debug.Log($"[CinematicTrigger] Случайная задержка: {calculatedDelay:F1}с (окно: {availableWindow:F1}с)");
                }
                else
                {
                    calculatedDelay = minDelay;
                }
            }
            
            if (calculatedDelay > 0)
                StartCoroutine(DelayedTrigger(calculatedDelay));
            else
                PlayGraph();
        }

        private IEnumerator DelayedTrigger(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            PlayGraph();
        }

        private void PlayGraph()
        {
            if (graphToPlay == null) return;
            
            var cinematicPlayer = FindObjectOfType<CinematicPlayer>();
            if (cinematicPlayer == null)
            {
                var go = new GameObject("CinematicPlayer");
                cinematicPlayer = go.AddComponent<CinematicPlayer>();
            }
            
            var mode = forceBackground ? ExecutionMode.Background : executionMode;
            cinematicPlayer.Play(graphToPlay, mode);
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnDayChanged -= OnDayChanged;
                TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, Vector3.one);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
                $"Trigger: {triggerType}");
        }
#endif
    }
}