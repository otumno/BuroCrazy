using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Экран отстранения Директора. Показывается при превышении лимита страйков.
    /// Самодостаточный префаб-синглтон: инстанцируется из Resources/EndingSystem/DismissalScreen.prefab.
    /// Две кнопки: "Загрузить" (перезагрузить утро текущего дня) и "Главное меню".
    /// </summary>
    public class DismissalScreenUI : MonoBehaviour
    {
        public static DismissalScreenUI Instance { get; private set; }

        private const string PrefabResourcePath = "EndingSystem/DismissalScreen";

        [Header("UI элементы")]
        public Image backgroundImage;
        public TextMeshProUGUI messageText;
        public Button reloadButton;
        public Button mainMenuButton;
        public TextMeshProUGUI reloadButtonText;
        public TextMeshProUGUI mainMenuButtonText;
        public CanvasGroup canvasGroup;

        [Header("Текст по умолчанию")]
        [TextArea(2, 5)]
        public string defaultMessage = "Внимание! Директор отстранён от должности за превышение допустимого количества ошибок.\nБюро закрыто на переаттестацию.";

        private Action onReload;
        private Action onMenu;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            if (reloadButton != null) reloadButton.onClick.AddListener(OnReloadClicked);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMenuClicked);
            if (reloadButtonText != null) reloadButtonText.text = "Загрузить";
            if (mainMenuButtonText != null) mainMenuButtonText.text = "Главное меню";
            if (messageText != null) messageText.text = defaultMessage;
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

        public static DismissalScreenUI GetOrCreate()
        {
            if (Instance != null) return Instance;

            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[DismissalScreenUI] Префаб не найден в Resources/{PrefabResourcePath}.prefab");
                return null;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>();
            GameObject instance;
            if (canvas != null)
                instance = Instantiate(prefab, canvas.transform);
            else
                instance = Instantiate(prefab);

            return instance.GetComponent<DismissalScreenUI>();
        }

        public void ShowDismissal(Action onReloadCallback, Action onMenuCallback)
        {
            EnsureLoaded();

            onReload = onReloadCallback;
            onMenu = onMenuCallback;

            gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        private void EnsureLoaded()
        {
            if (Instance != null) return;
            GetOrCreate();
        }

        private void OnReloadClicked()
        {
            Debug.Log("[DismissalScreenUI] Загрузка сохранения.");
            try { onReload?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
            gameObject.SetActive(false);
        }

        private void OnMenuClicked()
        {
            Debug.Log("[DismissalScreenUI] Выход в главное меню.");
            try { onMenu?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
            gameObject.SetActive(false);
        }
    }
}
