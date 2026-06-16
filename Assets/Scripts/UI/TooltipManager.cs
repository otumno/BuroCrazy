using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI.Tooltips
{
    public class TooltipManager : MonoBehaviour
    {
        public static TooltipManager Instance { get; private set; }

        [Header("Шрифт")]
        [Tooltip("Шрифт TextMeshPro. Должен поддерживать нужные символы (например, кириллицу).")]
        public TMP_FontAsset tooltipFont;

        [Header("Внешний вид")]
        [Tooltip("Размер шрифта тултипа.")]
        public int fontSize = 18;
        [Tooltip("Цвет фона тултипа. По умолчанию прозрачный (без подложки). Поставьте Alpha > 0, если нужна подложка.")]
        public Color backgroundColor = new Color(0f, 0f, 0f, 0f);
        [Tooltip("Цвет текста тултипа.")]
        public Color textColor = Color.white;
        [Tooltip("Внутренние отступы фона от текста (в пикселях).")]
        public Vector2 backgroundPadding = new Vector2(4f, 2f);
        [Tooltip("Перенос текста по словам. По умолчанию выключен — текст в одну строку.")]
        public bool enableWordWrapping = false;
        [Tooltip("Максимальная ширина тултипа (если включён перенос). 0 = без ограничений.")]
        public float maxWidth = 0f;

        [Header("Позиция")]
        [Tooltip("Смещение тултипа относительно курсора (в пикселях). По умолчанию 0,-30 — тултип чуть ниже мыши.")]
        public Vector2 offset = new Vector2(0f, -30f);

        [Header("Поведение")]
        [Tooltip("Задержка перед показом тултипа при наведении (сек).")]
        public float displayDelay = 0.3f;

        private Canvas tooltipCanvas;
        private TextMeshProUGUI tooltipText;
        private RectTransform tooltipRect;

        private TooltipTarget currentTarget;
        private float showTimer = 0f;
        private bool isVisible = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[TooltipManager] Duplicate TooltipManager found on '{gameObject.name}' (path '{GetFullPath()}'). Existing instance is on '{Instance.gameObject.name}' (path '{Instance.GetFullPath()}'). Destroying duplicate.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Debug.Log($"[TooltipManager] Awake on '{gameObject.name}' (path '{GetFullPath()}')");
        }

        private string GetFullPath()
        {
            var t = transform;
            var sb = new System.Text.StringBuilder(t.name);
            while (t.parent != null)
            {
                t = t.parent;
                sb.Insert(0, t.name + "/");
            }
            return sb.ToString();
        }

        private void Start()
        {
            Debug.Log($"[TooltipManager] Start on '{gameObject.name}': font={(tooltipFont != null ? tooltipFont.name : "<null>")}, fontSize={fontSize}, offset={offset}, displayDelay={displayDelay}, bg.a={backgroundColor.a}");
            // Авто-выбор шрифта с поддержкой кириллицы, если не задан в инспекторе.
            if (tooltipFont == null)
            {
                tooltipFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")
                            ?? Resources.Load<TMP_FontAsset>("Fonts/TagesschriftCyrillic-Regular SDF")
                            ?? Resources.Load<TMP_FontAsset>("Fonts/Ramona-Bold SDF")
                            ?? Resources.Load<TMP_FontAsset>("Fonts/Ramona-Light SDF");
            }

            CreateTooltipUI();
            if (tooltipCanvas != null) tooltipCanvas.enabled = false;
        }

        private void CreateTooltipUI()
        {
            // Создаём Canvas
            var canvasGO = new GameObject("TooltipCanvas");
            canvasGO.transform.SetParent(transform, false);
            tooltipCanvas = canvasGO.AddComponent<Canvas>();
            tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            tooltipCanvas.overrideSorting = true;
            tooltipCanvas.sortingOrder = short.MaxValue; // поверх всего UI

            // Фон
            var bgGO = new GameObject("TooltipBackground");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgImage = bgGO.AddComponent<Image>();
            bgImage.color = backgroundColor;
            bgImage.raycastTarget = false;
            // Если фон прозрачный — отключаем Image, чтобы он не мешал рейкасту и не "съедал" клики.
            bgImage.enabled = backgroundColor.a > 0.001f;
            tooltipRect = bgGO.GetComponent<RectTransform>();
            tooltipRect.anchorMin = Vector2.zero;
            tooltipRect.anchorMax = Vector2.zero;
            tooltipRect.pivot = new Vector2(0.5f, 0.5f);
            tooltipRect.sizeDelta = new Vector2(200, 50);

            // Текст внутри фона
            var textGO = new GameObject("TooltipText");
            textGO.transform.SetParent(bgGO.transform, false);
            tooltipText = textGO.AddComponent<TextMeshProUGUI>();
            if (tooltipFont != null) tooltipText.font = tooltipFont;
            tooltipText.fontSize = fontSize;
            tooltipText.color = textColor;
            tooltipText.alignment = TextAlignmentOptions.Center;
            tooltipText.raycastTarget = false;
            tooltipText.enableWordWrapping = enableWordWrapping;
            var textRect = tooltipText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = backgroundPadding;
            textRect.offsetMax = -backgroundPadding;

            tooltipCanvas.enabled = false;
        }

        private void Update()
        {
            if (Camera.main == null || tooltipCanvas == null) return;

            // Отладочный хоткей: F12 — принудительно показать тултип "TEST" в позиции мыши.
            // Если это работает — значит canvas/шрифт/позиция в порядке, и проблема в детекторе hover'а.
            if (Input.GetKeyDown(KeyCode.F12))
            {
                Debug.Log($"[TooltipManager] F12: canvas.enabled={tooltipCanvas.enabled}, sortingOrder={tooltipCanvas.sortingOrder}, font={(tooltipText.font != null ? tooltipText.font.name : "<null>")}, bg.enabled={(tooltipRect.GetComponent<UnityEngine.UI.Image>()?.enabled ?? false)}");
                tooltipText.text = "TEST";
                tooltipText.ForceMeshUpdate();
                Vector2 pref = tooltipText.GetRenderedValues(false);
                tooltipRect.sizeDelta = new Vector2(pref.x + backgroundPadding.x * 2f, pref.y + backgroundPadding.y * 2f);
                tooltipCanvas.enabled = true;
                isVisible = true;
                UpdatePosition();
            }

            UpdatePosition();
            UpdateTimers();
        }

        private void UpdateTimers()
        {
            bool hasActive = currentTarget != null && currentTarget.IsHovered();

            if (hasActive)
            {
                if (!isVisible && showTimer > 0f && Time.unscaledTime >= showTimer)
                {
                    ShowTooltip();
                }
            }
            else
            {
                if (isVisible) StartHide();
            }
        }

        private void UpdatePosition()
        {
            if (!isVisible || tooltipRect == null) return;
            // Для ScreenSpaceOverlay canvas: world space = screen pixels.
            tooltipRect.position = new Vector3(Input.mousePosition.x + offset.x,
                                               Input.mousePosition.y + offset.y,
                                               0f);
        }

        private void ShowTooltip()
        {
            if (tooltipCanvas == null || tooltipText == null || currentTarget == null) return;
            string text = currentTarget.tooltipText;
            if (string.IsNullOrEmpty(text)) { StartHide(); return; }

            tooltipText.text = text;
            tooltipText.ForceMeshUpdate();
            Vector2 preferred = tooltipText.GetRenderedValues(false);
            // Если перенос выключен, ресайзим под фактический размер текста.
            // Если включён — обрезаем по maxWidth (если задан).
            float width = preferred.x;
            float height = preferred.y;
            if (enableWordWrapping && maxWidth > 0f && width > maxWidth)
            {
                width = maxWidth;
            }
            tooltipRect.sizeDelta = new Vector2(width + backgroundPadding.x * 2f,
                                               height + backgroundPadding.y * 2f);

            tooltipCanvas.enabled = true;
            isVisible = true;
        }

        private void StartHide()
        {
            if (tooltipCanvas != null) tooltipCanvas.enabled = false;
            isVisible = false;
            if (tooltipText != null) tooltipText.text = "";
        }

        public void RegisterHover(TooltipTarget target)
        {
            if (target == null) return;
            if (currentTarget == target) return;
            currentTarget = target;
            showTimer = Time.unscaledTime + displayDelay;
        }

        public void UnregisterHover(TooltipTarget target)
        {
            if (currentTarget != target) return;
            currentTarget = null;
            showTimer = 0f;
            if (isVisible) StartHide();
        }
    }
}
