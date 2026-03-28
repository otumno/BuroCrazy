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

        [Header("Система Репутации (HP)")]
        public float currentReputation = 100f;
        public float baseMaxReputation = 100f;
        public float reputationPerRegion = 50f;
        
        [Header("Баланс: Успехи и Лечение")]
        public int successesNeededForOneHP = 5;
        private int currentSuccessStreak = 0;
        public float archivistHealAmount = 15f;
        [Tooltip("Процент от Max HP, который восстанавливается в конце дня (0.2 = 20%)")]
        [Range(0f, 1f)] public float endOfDayHealPercentage = 0.2f;
        
        [Header("Баланс: Урон (Ошибки)")]
        public float damageIntern = 2f;
        public float damageRegistrar = 3f; // FIXED: Separate penalty for Registrar
        public float damageClerk = 5f;
        public float damageCashier = 10f;
        public float damageDirector = 20f;
        public float corruptionDamageMultiplier = 0.1f; // 1 HP за каждые $10 коррупции

        public float GetMaxReputation()
        {
            int regions = ProgressionManager.Instance != null ? ProgressionManager.Instance.GetCapturedRegionsCount() : 0;
            return baseMaxReputation + (regions * reputationPerRegion);
        }

        public void RegisterSuccess(StaffController.Role role)
        {
            // Архивариус работает как медик
            if (role == StaffController.Role.Archivist)
            {
                HealReputation(archivistHealAmount);
                return;
            }

            currentSuccessStreak++;
            if (currentSuccessStreak >= successesNeededForOneHP)
            {
                currentSuccessStreak = 0;
                HealReputation(1f);
            }
        }

        public void RegisterFailure(StaffController.Role role)
        {
            float damage = damageClerk; // По умолчанию
            switch (role)
            {
                case StaffController.Role.Intern: damage = damageIntern; break;
                case StaffController.Role.Registrar: damage = damageRegistrar; break; // FIXED: Use specific damage
                case StaffController.Role.Clerk: damage = damageClerk; break;
                case StaffController.Role.Cashier:
                case StaffController.Role.Accountant: damage = damageCashier; break;
                case StaffController.Role.Director: damage = damageDirector; break;
            }
            TakeDamage(damage, $"Ошибка сотрудника ({role})");
        }

        public void RegisterCorruption(int shadowMoneyAmount)
        {
            float damage = shadowMoneyAmount * corruptionDamageMultiplier;
            TakeDamage(damage, "Теневые доходы (Коррупция)");
        }

        private void HealReputation(float amount)
        {
            float max = GetMaxReputation();
            currentReputation = Mathf.Clamp(currentReputation + amount, 0f, max);
            Debug.Log($"<color=green>[Репутация]</color> Восстановлено {amount} HP. Текущее: {currentReputation}/{max}");
        }

        private void TakeDamage(float amount, string reason)
        {
            float maxHP = GetMaxReputation();
            if (maxHP <= 0f) return; // FIXED: Защита от страйка, если районов нет и база 0 (Режим обучения)

            currentReputation -= amount;
            Debug.Log($"<color=red>[Репутация]</color> Урон -{amount} HP ({reason}). Текущее: {currentReputation}/{maxHP}");
            
            if (currentReputation <= 0f)
            {
                currentReputation = maxHP; // Сброс после страйка
                AddStrike();
                Debug.LogWarning($"<color=red>СТРАЙК!</color> Репутация упала до нуля!");
            }
        }

        public void ApplyEndOfDayHeal()
        {
            float maxHP = GetMaxReputation();
            if (maxHP > 0f)
            {
                float healAmount = maxHP * endOfDayHealPercentage;
                HealReputation(healAmount);
                Debug.Log($"<color=green>[Конец Дня]</color> Характеристика Престижа (HP) восстановлена на {healAmount} единиц.");
            }
        }

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
            currentReputation = GetMaxReputation();
            currentSuccessStreak = 0;
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