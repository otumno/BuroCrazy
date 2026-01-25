using System.Collections;
using UnityEngine;
using TMPro;

namespace UI.Academy
{
    public class AcademyUI : MonoBehaviour
    {
        [Header("Панели")]
        public GameObject trainingPanel;
        public GameObject completionPanel;
        public GameObject kobayashiMaruPanel;

        [Header("Текст")]
        public TextMeshProUGUI instructionText;
        public TextMeshProUGUI feedbackText;
        public TextMeshProUGUI completionTitle;
        public TextMeshProUGUI completionMessage;

        [Header("Кнопки")]
        public UnityEngine.UI.Button skipButton;

        private Coroutine feedbackCoroutine;

        public void ShowTraining(bool show)
        {
            if (trainingPanel != null)
            {
                trainingPanel.SetActive(show);
            }
        }

        public void ShowInstruction(string text)
        {
            if (instructionText != null)
            {
                instructionText.text = text;
            }
        }

        public void ShowFeedback(string message, Color color)
        {
            if (feedbackText != null)
            {
                feedbackText.text = message;
                feedbackText.color = color;
            }

            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
            }
            feedbackCoroutine = StartCoroutine(HideFeedbackAfterDelay(2f));
        }

        private IEnumerator HideFeedbackAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (feedbackText != null)
            {
                feedbackText.text = "";
            }
        }

        public void ShowCompletionMessage()
        {
            if (completionPanel != null)
            {
                completionPanel.SetActive(true);
            }

            if (completionTitle != null)
            {
                completionTitle.text = "Обучение завершено!";
            }

            if (completionMessage != null)
            {
                completionMessage.text = "Теперь вы готовы к работе в Бюро.\nУдачи!";
            }
        }

        public void ShowKobayashiMaruMessage()
        {
            if (kobayashiMaruPanel != null)
            {
                kobayashiMaruPanel.SetActive(true);
            }
        }

        public void HideKobayashiMaruMessage()
        {
            if (kobayashiMaruPanel != null)
            {
                kobayashiMaruPanel.SetActive(false);
            }
        }

        public void OnSkipClicked()
        {
            if (Managers.Academy.AcademyScenarioManager.Instance != null)
            {
                Managers.Academy.AcademyScenarioManager.Instance.SkipTraining();
            }
        }

        public void OnContinueClicked()
        {
            if (completionPanel != null)
            {
                completionPanel.SetActive(false);
            }

            // Переход к игре - используем правильный метод
            Managers.MainUIManager.Instance?.StartOrResumeGameplay();
        }

        private void Start()
        {
            if (skipButton != null)
            {
                skipButton.onClick.AddListener(OnSkipClicked);
            }

            ShowTraining(false);
            completionPanel?.SetActive(false);
            kobayashiMaruPanel?.SetActive(false);
        }
    }
}
