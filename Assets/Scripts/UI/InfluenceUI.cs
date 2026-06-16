// Assets/Scripts/UI/InfluenceUI.cs
using TMPro;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// UI-счётчик Влияния. Подписывается на ProgressionManager.OnInfluenceChanged
    /// и обновляет текст. Вешать на объект с компонентом TextMeshProUGUI.
    /// </summary>
    public class InfluenceUI : MonoBehaviour
    {
        public static InfluenceUI Instance { get; private set; }

        [Header("UI Компоненты")]
        public TextMeshProUGUI influenceText;

        [Tooltip("Префаб эффекта изменения (опционально)")]
        public GameObject influenceEffectPrefab;

        [Header("Формат")]
        [Tooltip("Формат отображения. {0} = текущее значение")]
        public string textFormat = "Влияние: {0}";

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);
        }

        private void OnEnable()
        {
            if (ProgressionManager.Instance != null)
            {
                ProgressionManager.Instance.OnInfluenceChanged += OnInfluenceChanged;
                UpdateInfluenceText(ProgressionManager.Instance.GetInfluence());
            }
        }

        private void OnDisable()
        {
            if (ProgressionManager.Instance != null)
            {
                ProgressionManager.Instance.OnInfluenceChanged -= OnInfluenceChanged;
            }
        }

        private void OnInfluenceChanged(int newValue)
        {
            UpdateInfluenceText(newValue);
        }

        private void UpdateInfluenceText(int value)
        {
            if (influenceText != null)
            {
                influenceText.text = string.Format(textFormat, value);
            }
        }

        public void Refresh()
        {
            if (ProgressionManager.Instance != null)
            {
                UpdateInfluenceText(ProgressionManager.Instance.GetInfluence());
            }
        }
    }
}
