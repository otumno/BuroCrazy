using System.Collections;
using DG.Tweening;
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

        [Header("Настройки анимации")]
        [Tooltip("Длительность fade-in (сек). Если 0 — берётся из AIBalanceConfig.")]
        public float fadeInDuration = 0f;
        [Tooltip("Длительность показа (сек). Если 0 — берётся из AIBalanceConfig.")]
        public float holdDuration = 0f;
        [Tooltip("Длительность fade-out (сек). Если 0 — берётся из AIBalanceConfig.")]
        public float fadeOutDuration = 0f;

        private Coroutine sequenceCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

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

        public void ShowArcTitle(string arcTitle, AudioClip sound = null)
        {
            if (string.IsNullOrEmpty(arcTitle)) return;

            EnsureLoaded();
            if (Instance == null) return;

            if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = StartCoroutine(PlayArcTitle(arcTitle, sound));
        }

        private void EnsureLoaded()
        {
            if (Instance != null) return;
            GetOrCreate();
        }

        private IEnumerator PlayArcTitle(string arcTitle, AudioClip sound)
        {
            float fadeIn = fadeInDuration > 0f ? fadeInDuration : (AIBalanceConfig.Instance != null ? AIBalanceConfig.Instance.arcTitleFadeIn : 1f);
            float hold = holdDuration > 0f ? holdDuration : (AIBalanceConfig.Instance != null ? AIBalanceConfig.Instance.arcTitleHold : 3f);
            float fadeOut = fadeOutDuration > 0f ? fadeOutDuration : (AIBalanceConfig.Instance != null ? AIBalanceConfig.Instance.arcTitleFadeOut : 2f);

            gameObject.SetActive(true);
            if (canvasGroup == null) yield break;

            if (titleText != null) titleText.text = arcTitle;

            if (sound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayAudioClip2D(sound);
            }
            else if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(SoundID.Arc_Title);
            }

            canvasGroup.alpha = 0f;
            yield return canvasGroup.DOFade(1f, fadeIn).SetUpdate(true).WaitForCompletion();

            yield return new WaitForSecondsRealtime(hold);

            yield return canvasGroup.DOFade(0f, fadeOut).SetUpdate(true).WaitForCompletion();

            gameObject.SetActive(false);
            sequenceCoroutine = null;
        }
    }
}
