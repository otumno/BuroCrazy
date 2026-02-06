using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI.Teletype
{
    [RequireComponent(typeof(RectTransform))]
    public class TeletypeTerminalBuilder : MonoBehaviour
    {
        [Header("Размеры")]
        [SerializeField] private Vector2 size = new Vector2(400, 180);
        [SerializeField] private float headerHeight = 30f;
        [SerializeField] private float messageHeight = 40f;

        [Header("Цвета")]
        [SerializeField] private Color panelColor = new Color(0.05f, 0.05f, 0.08f, 0.95f);
        [SerializeField] private Color headerColor = new Color(0.1f, 0.1f, 0.14f, 1f);
        [SerializeField] private Color messageColor = new Color(0.08f, 0.08f, 0.12f, 0.9f);
        [SerializeField] private Color textColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private Color headerTextColor = new Color(0.4f, 1f, 0.4f);

        private void Awake()
        {
            Build();
        }

        public void Build()
        {
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            var panelImage = gameObject.AddComponent<Image>();
            panelImage.color = panelColor;

            var vlg = gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 4;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            CreateHeader();
            CreateMessagesContainer();
        }

        private void CreateHeader()
        {
            var header = new GameObject("Header");
            header.transform.SetParent(transform, false);

            var rt = header.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size.x - 16, headerHeight);

            var image = header.AddComponent<Image>();
            image.color = headerColor;

            var text = CreateText(header, "/// ТЕЛЕТАЙП ///", headerTextColor, 14);
            //gameObject.AddComponent<TeletypeTerminalUI>().headerText = text;
        }

        private void CreateMessagesContainer()
        {
            var container = new GameObject("MessagesContainer");
            container.transform.SetParent(transform, false);

            var rt = container.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size.x - 16, messageHeight * 3 + 8);

            var vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 2;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var contentSizeFitter = container.AddComponent<ContentSizeFitter>();
            contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var prefab = CreateMessagePrefab();
            container.AddComponent<TeletypeTerminalUI>().messagePrefab = prefab;
        }

        private TextMeshProUGUI CreateText(GameObject parent, string text, Color color, int fontSize)
        {
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(parent.transform, false);

            var rt = textObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;

            return tmp;
        }

        private GameObject CreateMessagePrefab()
        {
            var prefab = new GameObject("MessagePrefab");
            prefab.SetActive(false);

            var rt = prefab.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size.x - 24, messageHeight);

            var image = prefab.AddComponent<Image>();
            image.color = messageColor;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(prefab.transform, false);

            var textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8, 0);
            textRt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.color = textColor;
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.richText = true;
            tmp.enableWordWrapping = true;

            var le = prefab.AddComponent<LayoutElement>();
            le.preferredHeight = messageHeight;
            le.flexibleWidth = 1;

            return prefab;
        }
    }
}
