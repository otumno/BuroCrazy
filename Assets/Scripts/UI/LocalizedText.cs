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
        private bool _subscribed;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            // Пустой ключ (например, в базовом префабе) — оставляем текст как есть.
            if (_localizedString.IsEmpty || _text == null || _subscribed)
                return;

            // Подписка сразу запрашивает строку, а дальше LocalizedString сам присылает её при смене языка.
            _localizedString.StringChanged += LocalizedString_StringChanged;
            _subscribed = true;
        }

        private void OnDisable()
        {
            _localizedString.StringChanged -= LocalizedString_StringChanged;
            _subscribed = false;
        }

        public void SetReference(string table, string key, params object[] arguments)
        {
            OnDisable();
            _localizedString.SetReference(table, key);
            _localizedString.Arguments = arguments;
            if (isActiveAndEnabled) OnEnable();
        }

        public void SetArguments(params object[] arguments)
        {
            _localizedString.Arguments = arguments;
            if (_subscribed) _localizedString.RefreshString();
        }

        private void LocalizedString_StringChanged(string value)
        {
            if (_text != null) _text.text = value;
        }
    }
}
