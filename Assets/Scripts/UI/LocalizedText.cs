using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace UI
{
    /// <summary>
    /// Подставляет в TMP_Text строку из таблицы локализации и обновляет её при смене языка.
    /// </summary>
    // Своя обёртка вместо LocalizeStringEvent: там на каждый текст нужно вручную вешать
    // UnityEvent на TMP_Text.set_text, а здесь достаточно выбрать ключ в инспекторе.
    // Без RequireComponent: TMP_Text абстрактный, Unity не сможет добавить его сам.
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField]
        private LocalizedString _localizedString = new LocalizedString();

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            // Пустой ключ (например, в базовом префабе) — оставляем текст как есть.
            if (_localizedString.IsEmpty)
                return;

            // Подписка сразу запрашивает строку, а дальше LocalizedString сам присылает её при смене языка.
            _localizedString.StringChanged += LocalizedString_StringChanged;
        }

        private void OnDisable()
        {
            _localizedString.StringChanged -= LocalizedString_StringChanged;
        }

        private void LocalizedString_StringChanged(string value) => _text.text = value;
    }
}
