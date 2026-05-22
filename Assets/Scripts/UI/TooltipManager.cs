using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using Clinch;

namespace UI.Tooltips
{
    public class TooltipManager : MonoBehaviour
    {
        public static TooltipManager Instance { get; private set; }

        [Header("Prefab Settings")]
        public GameObject tooltipPrefab;

        [Header("Position Settings")]
        [Tooltip("Смещение тултипа относительно курсора (в пикселях)")]
        public Vector2 offset = new Vector2(20f, -20f);

        [Tooltip("Pivot точки привязки (0,0 – лево-верх, 1,1 – право-низ)")]
        public Vector2 pivot = new Vector2(0.5f, 0.5f);

        [Header("Behaviour")]
        public float hideDelay = 0.3f;

        private Canvas tooltipCanvas;
        private TextMeshProUGUI tooltipText;
        private RectTransform tooltipRect;

        private TooltipTarget currentTarget;
        private ClinchTarget currentClinchTarget;
        private float showTimer = 0f;
        private float hideTimer = 0f;
        private bool isVisible = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            CreateTooltipUI();
            if (tooltipCanvas != null)
                tooltipCanvas.enabled = false;
        }

        private void CreateTooltipUI()
        {
            if (tooltipPrefab != null)
            {
                var go = Instantiate(tooltipPrefab, transform);
                tooltipCanvas = go.GetComponent<Canvas>();
                tooltipText = go.GetComponentInChildren<TextMeshProUGUI>();
                tooltipRect = tooltipText?.GetComponent<RectTransform>();
                if (tooltipCanvas != null) tooltipCanvas.enabled = false;
                ApplyPivot();
                return;
            }

            // Автосоздание, если префаба нет
            var canvasGO = new GameObject("TooltipCanvas");
            canvasGO.transform.SetParent(transform);
            tooltipCanvas = canvasGO.AddComponent<Canvas>();
            tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            tooltipCanvas.sortingOrder = 32767;

            var textGO = new GameObject("TooltipText");
            textGO.transform.SetParent(canvasGO.transform);
            tooltipText = textGO.AddComponent<TextMeshProUGUI>();
            tooltipText.fontSize = 24;
            tooltipText.color = Color.white;
            tooltipText.alignment = TextAlignmentOptions.Center;

            tooltipRect = tooltipText.GetComponent<RectTransform>();
            tooltipRect.sizeDelta = new Vector2(400, 50);

            ApplyPivot();
            tooltipCanvas.enabled = false;
        }

        private void ApplyPivot()
        {
            if (tooltipRect == null) return;
            tooltipRect.pivot = pivot;
            tooltipRect.anchorMin = pivot;
            tooltipRect.anchorMax = pivot;
            tooltipRect.anchoredPosition = Vector2.zero;
        }

        private void Update()
        {
            if (Camera.main == null) return;

            CheckClinchTarget();
            UpdateTimers();
            UpdatePosition();
        }

        private void CheckClinchTarget()
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var hit = Physics2D.OverlapPoint(mousePos);
            if (hit != null)
            {
                var clinch = hit.GetComponentInParent<ClinchTarget>();
                if (clinch != null && clinch.IsActive && !string.IsNullOrEmpty(clinch.tooltipText))
                {
                    if (currentClinchTarget != clinch)
                    {
                        ClearCurrent();
                        currentClinchTarget = clinch;
                        showTimer = Time.unscaledTime + clinch.displayDelay;
                    }
                    return;
                }
            }
            if (currentClinchTarget != null) ClearCurrent();
        }

        public void RegisterHover(TooltipTarget target)
        {
            if (target == null) return;
            if (currentTarget != null && currentTarget.priority > target.priority) return;
            ClearCurrent();
            currentTarget = target;
            showTimer = Time.unscaledTime + target.displayDelay;
        }

        public void UnregisterHover(TooltipTarget target)
        {
            if (currentTarget == target)
                hideTimer = Time.unscaledTime + hideDelay;
        }

        private void ClearCurrent()
        {
            currentTarget = null;
            currentClinchTarget = null;
            showTimer = 0f;
            hideTimer = 0f;
            if (isVisible) StartHide();
        }

        private void UpdateTimers()
        {
            bool hasActive = (currentClinchTarget != null) || (currentTarget != null && currentTarget.IsHovered());

            if (hasActive)
            {
                hideTimer = 0f;
                if (!isVisible && showTimer > 0 && Time.unscaledTime >= showTimer)
                    ShowTooltip();
            }
            else
            {
                if (isVisible && hideTimer > 0 && Time.unscaledTime >= hideTimer)
                    StartHide();
            }
        }

        private void UpdatePosition()
        {
            if (!isVisible || tooltipRect == null || tooltipCanvas == null) return;
            Vector2 mousePos = Input.mousePosition;
            tooltipRect.position = mousePos + offset;
        }

        private void ShowTooltip()
        {
            if (tooltipCanvas == null || tooltipText == null) return;
            string text = GetTooltipText();
            if (string.IsNullOrEmpty(text))
            {
                StartHide();
                return;
            }

            tooltipText.text = text;
            tooltipCanvas.enabled = true;
            isVisible = true;

            tooltipText.ForceMeshUpdate();
            Vector2 preferred = tooltipText.GetRenderedValues(false);
            tooltipRect.sizeDelta = new Vector2(preferred.x + 20, preferred.y + 10);
        }

        private void StartHide()
        {
            if (tooltipCanvas != null)
                tooltipCanvas.enabled = false;
            isVisible = false;
            tooltipText.text = "";
            showTimer = 0f;
            hideTimer = 0f;
        }

        private string GetTooltipText()
        {
            if (currentClinchTarget != null && !string.IsNullOrEmpty(currentClinchTarget.tooltipText))
                return currentClinchTarget.tooltipText;
            if (currentTarget != null && !string.IsNullOrEmpty(currentTarget.tooltipText))
                return currentTarget.tooltipText;
            return "";
        }

        public void TestShow(string text = "Test Tooltip")
        {
            if (tooltipCanvas == null) CreateTooltipUI();
            tooltipText.text = text;
            tooltipCanvas.enabled = true;
            isVisible = true;
            UpdatePosition();
            Debug.Log("[TooltipManager] Test tooltip shown");
        }
    }
}