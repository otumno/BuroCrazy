using System;
using System.Collections;
using System.Collections.Generic;
using Data;
using Gameplay;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// UI-компонент финальной книги учёта.
    /// Самодостаточный префаб-синглтон: инстанцируется из Resources/EndingSystem/EndingBook.prefab.
    /// Показывает 4 страницы: статистика → название → описание → кнопка выхода.
    /// </summary>
    public class EndingBookUI : MonoBehaviour
    {
        public static EndingBookUI Instance { get; private set; }

        private const string PrefabResourcePath = "EndingSystem/EndingBook";

        [Header("UI элементы")]
        public Image backgroundImage;
        public Image illustrationImage;
        public TextMeshProUGUI pageTitleText;
        public TextMeshProUGUI pageBodyText;
        public CanvasGroup canvasGroup;
        public Button exitButton;
        public TextMeshProUGUI exitButtonText;

        [Header("Страницы")]
        [Tooltip("Длительность показа каждой страницы (сек)")]
        public float pageDuration = 3f;

        private EndingEntry currentEntry;
        private Action onExit;
        private Coroutine sequenceCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            if (exitButton != null)
            {
                exitButton.gameObject.SetActive(false);
                exitButton.onClick.AddListener(OnExitClicked);
            }

            if (canvasGroup != null) canvasGroup.alpha = 0f;

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Editor-only: сбросить статическую ссылку на Instance (используется EndingSystemBuilder
        /// после сохранения префаба, чтобы Awake временного GameObject не оставлял
        /// ссылку на уничтожаемый объект).
        /// </summary>
        public static void ResetStaticInstanceForBuild()
        {
            Instance = null;
        }

        public static EndingBookUI GetOrCreate()
        {
            if (Instance != null) return Instance;

            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[EndingBookUI] Префаб не найден в Resources/{PrefabResourcePath}.prefab");
                return null;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>();
            GameObject instance;
            if (canvas != null)
                instance = Instantiate(prefab, canvas.transform);
            else
                instance = Instantiate(prefab);

            return instance.GetComponent<EndingBookUI>();
        }

        public void ShowEnding(EndingEntry entry, Action onExitCallback)
        {
            if (entry == null)
            {
                Debug.LogError("[EndingBookUI] entry == null");
                return;
            }

            EnsureLoaded();

            currentEntry = entry;
            onExit = onExitCallback;

            gameObject.SetActive(true);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (illustrationImage != null) illustrationImage.gameObject.SetActive(false);
            if (exitButton != null) exitButton.gameObject.SetActive(false);

            if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = StartCoroutine(PlaySequence());
        }

        private void EnsureLoaded()
        {
            if (Instance != null) return;
            GetOrCreate();
        }

        private IEnumerator PlaySequence()
        {
            // Страница 1 — статистика по чертам
            yield return ShowStatsPage();
            yield return new WaitForSecondsRealtime(pageDuration);

            // Страница 2 — название концовки
            yield return ShowTitlePage();
            yield return new WaitForSecondsRealtime(pageDuration);

            // Страница 3 — описание + иллюстрация
            yield return ShowDescriptionPage();
            yield return new WaitForSecondsRealtime(pageDuration);

            // Страница 4 — кнопка выхода
            yield return ShowExitPage();
        }

        private IEnumerator FadeIn(float duration = 0.5f)
        {
            if (canvasGroup == null) yield break;
            float t = 0f;
            canvasGroup.alpha = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(t / duration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        private IEnumerator ShowStatsPage()
        {
            if (pageTitleText != null) pageTitleText.text = "ИТОГИ ВАШЕЙ КАРЬЕРЫ";
            if (pageBodyText != null)
            {
                string body = BuildStatsBody();
                pageBodyText.text = body;
            }
            if (illustrationImage != null) illustrationImage.gameObject.SetActive(false);
            yield return FadeIn();
        }

        private IEnumerator ShowTitlePage()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (pageTitleText != null)
                pageTitleText.text = currentEntry != null ? currentEntry.displayName : "Концовка";
            if (pageBodyText != null)
                pageBodyText.text = "";
            if (illustrationImage != null) illustrationImage.gameObject.SetActive(false);
            yield return FadeIn();
        }

        private IEnumerator ShowDescriptionPage()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (pageTitleText != null)
                pageTitleText.text = currentEntry != null ? currentEntry.displayName : "";
            if (pageBodyText != null)
                pageBodyText.text = currentEntry != null ? currentEntry.description : "";
            if (illustrationImage != null)
            {
                illustrationImage.sprite = currentEntry != null ? currentEntry.endingImage : null;
                illustrationImage.gameObject.SetActive(currentEntry != null && currentEntry.endingImage != null);
            }
            yield return FadeIn();
        }

        private IEnumerator ShowExitPage()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (pageTitleText != null) pageTitleText.text = "Конец";
            if (pageBodyText != null) pageBodyText.text = "Спасибо за службу в Бюро.";
            if (illustrationImage != null) illustrationImage.gameObject.SetActive(false);
            yield return FadeIn();

            if (exitButton != null)
            {
                exitButton.gameObject.SetActive(true);
                if (exitButtonText != null) exitButtonText.text = "Выйти в меню";
            }
        }

        private string BuildStatsBody()
        {
            var lines = new List<string>();
            if (TraitManager.Instance != null)
            {
                lines.Add($"Закон: {TraitManager.Instance.GetTraitScore(TraitManager.TRAIT_LAW)}");
                lines.Add($"Сострадание: {TraitManager.Instance.GetTraitScore(TraitManager.TRAIT_EMPATHY)}");
                lines.Add($"Маска: {TraitManager.Instance.GetTraitScore(TraitManager.TRAIT_MASK)}");
                lines.Add($"Амбиции: {TraitManager.Instance.GetTraitScore(TraitManager.TRAIT_AMBITION)}");
            }
            else
            {
                lines.Add("Черты личности недоступны.");
            }

            if (DirectorManager.Instance != null)
            {
                lines.Add("");
                int limit = Gameplay.AIBalanceConfig.Instance != null ? Gameplay.AIBalanceConfig.Instance.dismissalStrikeLimit : 5;
                lines.Add($"Ошибок накоплено: {DirectorManager.Instance.currentStrikes} / {limit}");
            }

            return string.Join("\n", lines);
        }

        private void OnExitClicked()
        {
            Debug.Log("[EndingBookUI] Выход в меню по кнопке.");
            try { onExit?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }

            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (exitButton != null) exitButton.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }
    }
}
