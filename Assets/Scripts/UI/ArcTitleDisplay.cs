using System.Collections;
using Gameplay;
using Managers;
using Scriptables.Audio;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Титр арки. Показывается при старте каждого диалога, принадлежащего арке.
    /// Самодостаточный префаб-синглтон: инстанцируется из Resources/EndingSystem/ArcTitle.prefab.
    /// Неблокирующий: показывается поверх диалога с высоким sortingOrder.
    /// </summary>
    public class ArcTitleDisplay : MonoBehaviour
    {
        public static ArcTitleDisplay Instance { get; private set; }

        private const string PrefabResourcePath = "EndingSystem/ArcTitle";

        [Header("UI элементы")]
        public CanvasGroup canvasGroup;
        public TextMeshProUGUI titleText;
        [Tooltip("Опциональный подзаголовок (в текущей версии обычно пуст).")]
        public TextMeshProUGUI subtitleText;

        [Header("Настройки анимации (сек)")]
        [Tooltip("Задержка перед появлением титра. Если 0 — берётся из AIBalanceConfig.arcTitleInitialDelay.")]
        public float initialDelay = 0f;
        [Tooltip("Длительность fade-in. Если 0 — берётся из AIBalanceConfig.arcTitleFadeIn.")]
        public float fadeInDuration = 0f;
        [Tooltip("Длительность показа. Если 0 — берётся из AIBalanceConfig.arcTitleHold.")]
        public float holdDuration = 0f;
        [Tooltip("Длительность fade-out. Если 0 — берётся из AIBalanceConfig.arcTitleFadeOut.")]
        public float fadeOutDuration = 0f;

        private Coroutine sequenceCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (canvasGroup != null) canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Editor-only: сбросить статическую ссылку на Instance (используется EndingSystemBuilder).
        /// </summary>
        public static void ResetStaticInstanceForBuild()
        {
            Instance = null;
        }

        public static ArcTitleDisplay GetOrCreate()
        {
            if (Instance != null) return Instance;

            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[ArcTitleDisplay] Префаб не найден в Resources/{PrefabResourcePath}.prefab — титры арок будут пропущены.");
                return null;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>();
            GameObject instance;
            if (canvas != null)
                instance = Instantiate(prefab, canvas.transform);
            else
                instance = Instantiate(prefab);

            var canvasComponent = instance.GetComponent<Canvas>();
            if (canvasComponent != null) canvasComponent.sortingOrder = 9999;

            return instance.GetComponent<ArcTitleDisplay>();
        }

        /// <summary>
        /// Показать титр арки. Обратно совместимая сигнатура: (title, sound).
        /// Перегрузка с подзаголовком: (title, subtitle, sound).
        /// </summary>
        public void ShowArcTitle(string arcTitle, AudioClip sound = null)
        {
            ShowArcTitle(arcTitle, string.Empty, sound);
        }

        public void ShowArcTitle(string arcTitle, string subtitle, AudioClip sound = null)
        {
            if (string.IsNullOrEmpty(arcTitle)) return;

            EnsureLoaded();
            if (Instance == null) return;

            if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = StartCoroutine(PlayArcTitle(arcTitle, subtitle, sound));
        }

        private void EnsureLoaded()
        {
            if (Instance != null) return;
            GetOrCreate();
        }

        private IEnumerator PlayArcTitle(string arcTitle, string subtitle, AudioClip sound)
        {
            float delay = initialDelay > 0f ? initialDelay :
                (AIBalanceConfig.Instance != null ? AIBalanceConfig.Instance.arcTitleInitialDelay : 2f);
            float fadeIn = fadeInDuration > 0f ? fadeInDuration :
                (AIBalanceConfig.Instance != null ? AIBalanceConfig.Instance.arcTitleFadeIn : 1f);
            float hold = holdDuration > 0f ? holdDuration :
                (AIBalanceConfig.Instance != null ? AIBalanceConfig.Instance.arcTitleHold : 3f);
            float fadeOut = fadeOutDuration > 0f ? fadeOutDuration :
                (AIBalanceConfig.Instance != null ? AIBalanceConfig.Instance.arcTitleFadeOut : 2f);

            gameObject.SetActive(true);
            if (canvasGroup == null) yield break;

            if (titleText != null) titleText.text = arcTitle;
            if (subtitleText != null)
            {
                subtitleText.text = subtitle ?? string.Empty;
                subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            }

            if (sound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayAudioClip2D(sound);
            }
            else if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(SoundID.Arc_Title);
            }

            // Начальное состояние — скрыт
            canvasGroup.alpha = 0f;

            // Задержка перед появлением (используем unscaled, чтобы пауза не блокировала)
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            // Fade In
            float timer = 0f;
            while (timer < fadeIn)
            {
                timer += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeIn);
                yield return null;
            }
            canvasGroup.alpha = 1f;

            // Показ
            if (hold > 0f)
            {
                yield return new WaitForSecondsRealtime(hold);
            }

            // Fade Out
            timer = 0f;
            while (timer < fadeOut)
            {
                timer += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeOut);
                yield return null;
            }
            canvasGroup.alpha = 0f;

            gameObject.SetActive(false);
            sequenceCoroutine = null;
        }
    }
}
