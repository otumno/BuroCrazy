using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UI.Academy;

namespace Managers.Academy
{
    public class AcademyScenarioManager : MonoBehaviour
    {
        public static AcademyScenarioManager Instance { get; private set; }

        [Header("Сценарии обучения")]
        public List<AcademyScenario> allScenarios;

        [Header("Состояние")]
        public AcademyScenario currentScenario;
        public int currentStepIndex = 0;
        public bool isTrainingActive = false;
        public bool hasPassedKobayashiMaru = false;

        [Header("Настройки")]
        public float cardboardDecorationScale = 0.7f; // Картонные декорации меньше реальных
        public float actorOveractingMultiplier = 1.5f; // Актеры переигрывают

        [Header("UI")]
        public AcademyUI academyUI;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void StartTraining()
        {
            if (allScenarios == null || allScenarios.Count == 0)
            {
                Debug.LogError("[AcademyScenarioManager] Сценарии обучения не настроены!");
                return;
            }

            currentScenario = allScenarios[0];
            currentStepIndex = 0;
            isTrainingActive = true;
            hasPassedKobayashiMaru = false;

            if (academyUI != null)
            {
                academyUI.ShowTraining(true);
            }

            StartCoroutine(RunScenario());
        }

        private IEnumerator RunScenario()
        {
            while (currentStepIndex < currentScenario.steps.Count)
            {
                var step = currentScenario.steps[currentStepIndex];

                Debug.Log($"[Academy] Шаг {currentStepIndex + 1}: {step.stepName}");

                if (academyUI != null)
                {
                    academyUI.ShowInstruction(step.instructionText);
                }

                // Показываем картонную декорацию если есть
                if (step.cardboardDecoration != null)
                {
                    ShowCardboardDecoration(step.cardboardDecoration);
                }

                // Ждем выполнения условия или таймаута
                yield return StartCoroutine(WaitForStepCompletion(step));

                currentStepIndex++;

                yield return new WaitForSeconds(0.5f);
            }

            // Обучение завершено - проверяем Кобаяши Мару
            CheckKobayashiMaru();
        }

        private IEnumerator WaitForStepCompletion(AcademyScenario.AcademyStep step)
        {
            float elapsedTime = 0f;
            float timeout = step.timeout > 0 ? step.timeout : 30f;

            while (elapsedTime < timeout)
            {
                if (IsStepConditionMet(step))
                {
                    if (academyUI != null)
                    {
                        academyUI.ShowFeedback("Отлично!", Color.green);
                    }
                    yield break;
                }

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Таймаут - если это не критичный шаг, продолжаем
            if (!step.isCritical)
            {
                if (academyUI != null)
                {
                    academyUI.ShowFeedback("Ладно, пойдем дальше...", Color.yellow);
                }
            }
            else
            {
                if (academyUI != null)
                {
                    academyUI.ShowFeedback("Провал! Но система обойдена.", Color.cyan);
                }
            }
        }

        private bool IsStepConditionMet(AcademyScenario.AcademyStep step)
        {
            // Проверяем условия выполнения шага
            // Это будет реализовано в зависимости от типа шага
            return step.condition?.Invoke() ?? true;
        }

        private void ShowCardboardDecoration(GameObject decoration)
        {
            if (decoration != null)
            {
                var deco = Instantiate(decoration);
                deco.transform.localScale *= cardboardDecorationScale;

                Destroy(deco, 10f);
            }
        }

        private void CheckKobayashiMaru()
        {
            // Проверка на "непроходимый" тест
            if (hasPassedKobayashiMaru)
            {
                Debug.Log("[Academy] Кобаяши Мару пройден! Система обойдена.");

                if (academyUI != null)
                {
                    academyUI.ShowKobayashiMaruMessage();
                }

                // Выдаем бонус
                UnlockBonus();
            }

            FinishTraining();
        }

        private void UnlockBonus()
        {
            // Бонус за прохождение Кобаяши Мару
            // Например: дополнительное влияние или особый предмет
            if (Managers.GameLifecycleManager.Instance != null)
            {
                // Можно добавить специальный флаг
            }
        }

        private void FinishTraining()
        {
            isTrainingActive = false;

            if (academyUI != null)
            {
                academyUI.ShowTraining(false);
                academyUI.ShowCompletionMessage();
            }

            // Вызываем событие завершения обучения
            OnTrainingComplete?.Invoke(hasPassedKobayashiMaru);
        }

        public void SkipTraining()
        {
            StopAllCoroutines();
            FinishTraining();
        }

        public void SetKobayashiMaruPassed()
        {
            hasPassedKobayashiMaru = true;
        }

        public event System.Action<bool> OnTrainingComplete;
    }

    [System.Serializable]
    public class AcademyScenario
    {
        public string scenarioID;
        public string scenarioName;
        public List<AcademyStep> steps;

        [System.Serializable]
        public class AcademyStep
        {
            public string stepID;
            public string stepName;
            [TextArea(2, 5)]
            public string instructionText;

            [Header("Условие выполнения")]
            public System.Func<bool> condition;
            public float timeout = 0f;
            public bool isCritical = false;

            [Header("Картонная декорация (опционально)")]
            public GameObject cardboardDecoration;

            [Header("Переигрывающий актер")]
            public bool requireOveracting = false;
            public string overactingLine;
        }
    }
}
