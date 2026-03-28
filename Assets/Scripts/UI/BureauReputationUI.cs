using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Managers;

namespace UI
{
    public class BureauReputationUI : MonoBehaviour
    {
        [Header("UI Components")]
        [Tooltip("Слайдер (полоска) для визуализации HP")]
        public Slider hpSlider;
        
        [Tooltip("Текст для отображения значений (например, 24/150)")]
        public TextMeshProUGUI hpText;

        [Tooltip("Префикс перед цифрами")]
        public string titlePrefix = "Х.П.: ";

        [Header("Animation")]
        public float fillSpeed = 5f;

        private void Update()
        {
            if (DirectorManager.Instance == null) return;

            float currentHP = DirectorManager.Instance.currentReputation;
            float maxHP = DirectorManager.Instance.GetMaxReputation();

            // Если параметров еще нет (режим 0/0), показываем прочерки
            if (maxHP <= 0f)
            {
                if (hpText != null) hpText.text = $"{titlePrefix} -- / --";
                if (hpSlider != null) hpSlider.value = 1f; // Полная полоска для вида
                return;
            }

            // Плавное заполнение слайдера
            if (hpSlider != null)
            {
                float targetValue = Mathf.Clamp01(currentHP / maxHP);
                hpSlider.value = Mathf.Lerp(hpSlider.value, targetValue, Time.deltaTime * fillSpeed);
            }

            // Обновление текста
            if (hpText != null)
            {
                // Округляем до целых для красоты (CeilToInt чтобы не показывать 0, если осталось 0.5 HP)
                int displayCurrent = Mathf.CeilToInt(currentHP);
                int displayMax = Mathf.RoundToInt(maxHP);
                hpText.text = $"{titlePrefix}{displayCurrent} / {displayMax}";
            }
        }
    }
}