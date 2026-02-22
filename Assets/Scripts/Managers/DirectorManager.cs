using System;
using UnityEngine;
using System.Linq;

namespace Managers
{
    public class DirectorManager : MonoBehaviour
    {
        public static DirectorManager Instance { get; private set; }

        [Header("Состояние Директора")]
        public int currentStrikes = 0;

        private void Awake()
        {
            if (Instance == null) { Instance = this; }
            else if (Instance != this) { Destroy(this); }
        }

        public void AddStrike()
        {
            currentStrikes++;
            Debug.Log($"[DirectorManager] Получена ошибка! Всего ошибок: {currentStrikes}");
        }

        public void SetStrikes(int strikes)
        {
            currentStrikes = Mathf.Clamp(strikes, 0, 3);
            Debug.Log($"[DirectorManager] Установлено ошибок: {currentStrikes}");
        }

        public void PrepareDay()
        {
            // Логика подготовки к новому дню
        }

        public void ResetState()
        {
            currentStrikes = 0;
        }
        
        public void EvaluateEndOfDayStrikes(int dayIndex)
        {
            if (DocumentQualityManager.Instance == null || OrderManager.Instance == null)
                return;

            float averageError = DocumentQualityManager.Instance.GetCurrentAverageErrorRate();
            float allowedError = 1.0f; 

            // Теперь берем данные из OrderManager
            if (OrderManager.Instance.currentMandates.Any())
            {
                allowedError = OrderManager.Instance.currentMandates[0].allowedDirectorErrorRate;
            }

            if (averageError > allowedError)
            {
                AddStrike();
                Debug.LogWarning($"[End of Day] СТРАЙК! Среднее количество ошибок {averageError:P1} превысило норму {allowedError:P1}.");
            }
            
            DocumentQualityManager.Instance.ResetDay();
        }

        private void Start()
        {
            TimeManager.Instance.OnDayChanged += EvaluateEndOfDayStrikes;
        }

        private void OnDestroy()
        {
            if (TimeManager.Instance)
                TimeManager.Instance.OnDayChanged -= EvaluateEndOfDayStrikes;
        }
    }
}