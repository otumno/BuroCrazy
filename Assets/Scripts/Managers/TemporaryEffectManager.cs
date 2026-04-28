using System.Collections;
using System.Collections.Generic;
using Data.Calendar;
using UnityEngine;

namespace Managers
{
    public class TemporaryEffectManager : MonoBehaviour
    {
        public static TemporaryEffectManager Instance { get; private set; }

        private HashSet<TemporaryEffectType> activeEffects = new HashSet<TemporaryEffectType>();

        [Header("Cleaning Crew")]
        [SerializeField] private GameObject cleanerPrefab;
        [SerializeField] private int cleanersCount = 2;
        [SerializeField] private Transform[] spawnPoints;

        [Header("Clown")]
        [SerializeField] private GameObject clownMalePrefab;
        [SerializeField] private GameObject clownFemalePrefab;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            
            StartCoroutine(SubscribeToPeriodChanges());
        }
        
        private IEnumerator SubscribeToPeriodChanges()
        {
            while (TimeManager.Instance == null)
                yield return null;
            TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
            TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;
            Debug.Log("[TemporaryEffectManager] Успешно подписан на OnPeriodChanged");
        }

        private void OnEnable()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged; // отписываемся на всякий случай
                TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;
                // Debug.Log("[TemporaryEffectManager] Subscribed to OnPeriodChanged");
            }
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance != null)
                TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
        }

        private void OnPeriodChanged(PeriodSettings settings)
        {
            Debug.Log($"[TemporaryEffectManager] OnPeriodChanged: {settings.PeriodType}");
            CheckScheduledCalls();
            ClearAllEffects();
        }

        private void CheckScheduledCalls()
        {
            Debug.Log("[TemporaryEffectManager] CheckScheduledCalls вызван");
            if (StoryStateManager.Instance == null)
            {
                Debug.LogError("[TemporaryEffectManager] StoryStateManager.Instance == null!");
                return;
            }

            int cleaning = StoryStateManager.Instance.GetFlag("CLEANING_SCHEDULED");
            if (cleaning == 1)
            {
                Debug.Log("[TemporaryEffectManager] Спавн уборщиков по флагу");
                SpawnCleaningCrew();
                StoryStateManager.Instance.SetFlag("CLEANING_SCHEDULED", 0);
            }

            int clownClients = StoryStateManager.Instance.GetFlag("CLOWN_CLIENTS");
            if (clownClients == 1)
            {
                SpawnClown(TemporaryClownAI.ClownMode.Clients);
                StoryStateManager.Instance.SetFlag("CLOWN_CLIENTS", 0);
            }

            int clownStaff = StoryStateManager.Instance.GetFlag("CLOWN_STAFF");
            if (clownStaff == 1)
            {
                SpawnClown(TemporaryClownAI.ClownMode.Staff);
                StoryStateManager.Instance.SetFlag("CLOWN_STAFF", 0);
            }
        }

        public void SpawnClown(TemporaryClownAI.ClownMode mode)
        {
            bool isFemale = Random.value > 0.5f;
            GameObject prefab = isFemale ? clownFemalePrefab : clownMalePrefab;
            if (prefab == null)
            {
                Debug.LogError("[TemporaryEffectManager] Clown Prefab не назначен!");
                return;
            }

            Transform spawnPoint = spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints[Random.Range(0, spawnPoints.Length)]
                : WaveManager.Instance?.spawnPoint;

            if (spawnPoint == null)
            {
                Debug.LogError("[TemporaryEffectManager] Нет точки спавна для клоуна!");
                return;
            }

            GameObject clown = Instantiate(prefab, spawnPoint.position, Quaternion.identity);
            var ai = clown.GetComponent<TemporaryClownAI>();
            if (ai != null) ai.SetMode(mode);
        }

        public void ActivateEffect(TemporaryEffectType type)
        {
            if (activeEffects.Add(type))
            {
                // Debug.Log($"[TemporaryEffectManager] Effect {type} activated.");
            }
        }

        public void DeactivateEffect(TemporaryEffectType type)
        {
            activeEffects.Remove(type);
        }

        public bool IsEffectActive(TemporaryEffectType type) => activeEffects.Contains(type);

        public void ClearAllEffects()
        {
            activeEffects.Clear();
            // Debug.Log("[TemporaryEffectManager] All effects cleared.");
        }

        // Для отладки
        public List<TemporaryEffectType> GetActiveEffects() => new List<TemporaryEffectType>(activeEffects);

        public void SpawnCleaningCrew()
        {
            if (cleanerPrefab == null)
            {
                Debug.LogError("[TemporaryEffectManager] Cleaner Prefab не назначен!");
                return;
            }

            Transform spawnPoint = spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints[Random.Range(0, spawnPoints.Length)]
                : WaveManager.Instance?.spawnPoint;

            if (spawnPoint == null)
            {
                Debug.LogError("[TemporaryEffectManager] Нет точки спавна для уборщиков!");
                return;
            }

            for (int i = 0; i < cleanersCount; i++)
            {
                GameObject cleaner = Instantiate(cleanerPrefab, spawnPoint.position, Quaternion.identity);
                // Установить визуал (пол, спрайты) можно здесь или в префабе
            }
        }

    }
}