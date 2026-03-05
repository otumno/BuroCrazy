using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Scriptables.Progression;
using Managers;

namespace UI.Map
{
    [RequireComponent(typeof(Image), typeof(Button))]
    public class RegionSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Данные")]
        public RegionData regionData;

        [Header("Слои (Изображения)")]
        [SerializeField] private Image baseImage;
        [SerializeField] private Image highlightImage;
        [SerializeField] private Image outlineImage;
        [SerializeField] private Image shadowImage;
        [SerializeField] private Image lockIcon;

        [Header("Анимация")]
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float animationSpeed = 12f;

        private Image hitAreaImage;
        private Button selectButton;
        private Action<RegionData> onClickCallback;

        private Vector3 targetScale = Vector3.one;
        private float targetAlpha = 0f;
        private float currentAlpha = 0f;
        
        private bool isLocked = true; 
        private bool isHovered = false;
        
        // Новое свойство для выделения
        public bool isSelected { get; private set; }

        private void Awake()
        {
            hitAreaImage = GetComponent<Image>();
            selectButton = GetComponent<Button>();

            if (hitAreaImage != null) hitAreaImage.alphaHitTestMinimumThreshold = 0.1f;
            if (selectButton != null) selectButton.onClick.AddListener(OnClick);

            SetImageAlpha(outlineImage, 0f);
            SetImageAlpha(shadowImage, 0f);
            SetImageAlpha(lockIcon, 0f);
        }

        private void Update()
        {
            // Масштаб зависит ТОЛЬКО от наведения мыши
            targetScale = isHovered ? Vector3.one * hoverScale : Vector3.one;
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);

            // Прозрачность обводки и тени зависит от наведения ИЛИ от того, выбран ли регион
            targetAlpha = (isHovered || isSelected) ? 1f : 0f;
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.unscaledDeltaTime * animationSpeed);
            
            SetImageAlpha(outlineImage, currentAlpha);
            SetImageAlpha(shadowImage, currentAlpha); // Тень тоже оставляем для глубины, это красивее
            
            if (isLocked) SetImageAlpha(lockIcon, currentAlpha);
            else SetImageAlpha(lockIcon, 0f);
        }

        public void Setup(RegionData data, Action<RegionData> onClick)
        {
            regionData = data;
            onClickCallback = onClick;
            UpdateState();
        }

        public void UpdateState()
        {
            if (regionData == null || ProgressionManager.Instance == null) return;

            bool isUnlocked = ProgressionManager.Instance.IsRegionUnlocked(regionData.regionID);
            bool isPending = DocumentManager.Instance != null && DocumentManager.Instance.IsProjectDocPending(regionData.regionID);

            isLocked = !isUnlocked && !isPending;

            if (baseImage != null) baseImage.gameObject.SetActive(isLocked);
            if (highlightImage != null)
            {
                highlightImage.gameObject.SetActive(!isLocked);
                highlightImage.color = isPending ? new Color(1f, 1f, 0.7f, 1f) : Color.white;
            }
            
            if (lockIcon != null) lockIcon.gameObject.SetActive(true);
        }
        
        // Метод для управления выделением извне
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (isSelected) transform.SetAsLastSibling(); // Выводим на передний план при выделении
        }

        private void OnClick()
        {
            onClickCallback?.Invoke(regionData);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            transform.SetAsLastSibling();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySound(Scriptables.Audio.SoundID.UI_Hover);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
        }

        private void SetImageAlpha(Image img, float alpha)
        {
            if (img == null) return;
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }
}
