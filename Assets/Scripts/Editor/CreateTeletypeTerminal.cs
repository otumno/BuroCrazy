using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

namespace UI.Teletype.Editor
{
    public static class CreateTeletypeTerminal
    {
        [MenuItem("Tools/Create Teletype Terminal")]
        public static void Build()
        {
            var parent = FindOrCreateParent();
            if (parent == null) return;

            var root = CreateRoot(parent);
            CreateHeader(root);
            CreateMessagesContainer(root);
            SelectRoot(root);
        }

        private static Transform FindOrCreateParent()
        {
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[CreateTeletypeTerminal] Canvas не найден!");
                return null;
            }

            var gameObj = canvas.transform.Find("Game");
            if (gameObj == null)
            {
                gameObj = new GameObject("Game").transform;
                gameObj.SetParent(canvas.transform, false);
            }

            var buttons = gameObj.Find("InGameUIButtons");
            if (buttons == null)
            {
                buttons = new GameObject("InGameUIButtons").transform;
                buttons.SetParent(gameObj, false);
            }

            return buttons;
        }

        private static GameObject CreateRoot(Transform parent)
        {
            var go = new GameObject("[UI] TeletypeTerminal");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400, 180);
            rt.anchoredPosition = Vector2.zero;

            go.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 4;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            return go;
        }

        private static void CreateHeader(GameObject parent)
        {
            var header = new GameObject("Header");
            header.transform.SetParent(parent.transform, false);

            var rt = header.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(384, 30);

            header.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 1f);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(header.transform, false);
            var textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "/// ТЕЛЕТАЙП ///";
            tmp.color = new Color(0.4f, 1f, 0.4f);
            tmp.fontSize = 14;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;

            var ui = parent.AddComponent<TeletypeTerminalUI>();
            ui.headerText = tmp;
        }

        private static void CreateMessagesContainer(GameObject parent)
        {
            var container = new GameObject("MessagesContainer");
            container.transform.SetParent(parent.transform, false);

            var rt = container.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(384, 40);

            var vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 2;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var csf = container.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var prefab = CreateMessagePrefab();

            var ui = parent.GetComponent<TeletypeTerminalUI>();
            if (ui != null)
            {
                ui.messagesContainer = container.transform;
                ui.messagePrefab = prefab;
                ui.maxMessages = 3;
                ui.slideDuration = 0.3f;
            }
        }

        private static GameObject CreateMessagePrefab()
        {
            var prefab = new GameObject("MessagePrefab");
            prefab.SetActive(false);

            var rt = prefab.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(376, 40);

            prefab.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.9f);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(prefab.transform, false);
            var textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8, 0);
            textRt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "";
            tmp.color = new Color(0.5f, 1f, 0.5f);
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.richText = true;
            tmp.enableWordWrapping = true;

            prefab.AddComponent<LayoutElement>().preferredHeight = 40;

            return prefab;
        }

        private static void SelectRoot(GameObject root)
        {
            Selection.activeGameObject = root;
        }
    }
}
