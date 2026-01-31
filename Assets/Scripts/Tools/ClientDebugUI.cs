using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Characters;
using Data;

namespace Tools
{
    /// <summary>
    /// Простой UI для Debug меню (создаётся автоматически)
    /// </summary>
    [RequireComponent(typeof(ClientDebugMenu))]
    public class ClientDebugUI : MonoBehaviour
    {
        private ClientDebugMenu debugMenu;
        private GameObject panel;
        private Text infoText;
        private Button[] groupButtons;

        private void Awake()
        {
            debugMenu = GetComponent<ClientDebugMenu>();
            CreateUI();
        }

        private void CreateUI()
        {
            // Создаём Canvas
            var canvasGO = new GameObject("DebugCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Панель меню
            panel = new GameObject("DebugPanel");
            panel.transform.SetParent(canvasGO.transform, false);

            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0, 0);
            panelRect.pivot = new Vector2(0, 0);
            panelRect.anchoredPosition = new Vector2(10, 10);
            panelRect.sizeDelta = new Vector2(250, 280);

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);

            // Info текст
            var infoGO = new GameObject("InfoText");
            infoGO.transform.SetParent(panel.transform, false);
            var infoRect = infoGO.AddComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0, 1);
            infoRect.anchorMax = new Vector2(1, 1);
            infoRect.pivot = new Vector2(0.5f, 1);
            infoRect.anchoredPosition = new Vector2(0, -10);
            infoRect.sizeDelta = new Vector2(-20, 120);

            infoText = infoGO.AddComponent<Text>();
            infoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            infoText.fontSize = 14;
            infoText.color = Color.white;
            infoText.alignment = TextAnchor.UpperLeft;

            debugMenu.menuPanel = panel;
            debugMenu.infoText = infoText;

            // Кнопки для групп
            CreateGroupButtons(canvasGO.transform);

            panel.SetActive(false);
        }

        private void CreateGroupButtons(Transform parent)
        {
            var db = Resources.Load<ArchetypeDatabase>("Databases/ArchetypeDatabase");
            if (db == null || db.allArchetypes == null) return;

            var groups = new HashSet<string>();
            foreach (var a in db.allArchetypes)
            {
                if (a != null) groups.Add(a.groupID);
            }

            float yPos = -90;
            int index = 0;

            foreach (var group in groups)
            {
                var btnGO = new GameObject($"Btn_{group}");
                btnGO.transform.SetParent(panel.transform, false);

                var btnRect = btnGO.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0, 1);
                btnRect.anchorMax = new Vector2(1, 1);
                btnRect.pivot = new Vector2(0.5f, 1);
                btnRect.anchoredPosition = new Vector2(0, yPos);
                btnRect.sizeDelta = new Vector2(-20, 22);

                var btnImage = btnGO.AddComponent<Image>();
                btnImage.color = new Color(0.2f, 0.4f, 0.8f, 1f);

                var btn = btnGO.AddComponent<Button>();
                btn.onClick.AddListener(() => debugMenu.SpawnByGroup(group));

                // Текст кнопки
                var textGO = new GameObject("Text");
                textGO.transform.SetParent(btnGO.transform, false);
                var textRect = textGO.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;

                var text = textGO.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 12;
                text.color = Color.white;
                text.alignment = TextAnchor.MiddleCenter;
                text.text = $"Spawn {group}";

                yPos -= 25;
                index++;
            }

            // Кнопка "+10" для быстрого спавна
            var spawn10BtnGO = new GameObject("Btn_Spawn10");
            spawn10BtnGO.transform.SetParent(panel.transform, false);
            var spawn10BtnRect = spawn10BtnGO.AddComponent<RectTransform>();
            spawn10BtnRect.anchorMin = new Vector2(0, 1);
            spawn10BtnRect.anchorMax = new Vector2(1, 1);
            spawn10BtnRect.pivot = new Vector2(0.5f, 1);
            spawn10BtnRect.anchoredPosition = new Vector2(0, yPos);
            spawn10BtnRect.sizeDelta = new Vector2(-20, 22);

            var spawn10BtnImage = spawn10BtnGO.AddComponent<Image>();
            spawn10BtnImage.color = new Color(0.2f, 0.6f, 0.3f, 1f);

            var spawn10Btn = spawn10BtnGO.AddComponent<Button>();
            spawn10Btn.onClick.AddListener(() => debugMenu.SpawnMultiple(10));

            var spawn10TextGO = new GameObject("Text");
            spawn10TextGO.transform.SetParent(spawn10BtnGO.transform, false);
            var spawn10TextRect = spawn10TextGO.AddComponent<RectTransform>();
            spawn10TextRect.anchorMin = Vector2.zero;
            spawn10TextRect.anchorMax = Vector2.one;
            spawn10TextRect.sizeDelta = Vector2.zero;

            var spawn10Text = spawn10TextGO.AddComponent<Text>();
            spawn10Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            spawn10Text.fontSize = 12;
            spawn10Text.color = Color.white;
            spawn10Text.alignment = TextAnchor.MiddleCenter;
            spawn10Text.text = "+10 клиентов";

            yPos -= 25;

            // Кнопка "+100" для быстрого спавна
            var spawn100BtnGO = new GameObject("Btn_Spawn100");
            spawn100BtnGO.transform.SetParent(panel.transform, false);
            var spawn100BtnRect = spawn100BtnGO.AddComponent<RectTransform>();
            spawn100BtnRect.anchorMin = new Vector2(0, 1);
            spawn100BtnRect.anchorMax = new Vector2(1, 1);
            spawn100BtnRect.pivot = new Vector2(0.5f, 1);
            spawn100BtnRect.anchoredPosition = new Vector2(0, yPos);
            spawn100BtnRect.sizeDelta = new Vector2(-20, 22);

            var spawn100BtnImage = spawn100BtnGO.AddComponent<Image>();
            spawn100BtnImage.color = new Color(0.6f, 0.3f, 0.2f, 1f);

            var spawn100Btn = spawn100BtnGO.AddComponent<Button>();
            spawn100Btn.onClick.AddListener(() => debugMenu.SpawnMultiple(100));

            var spawn100TextGO = new GameObject("Text");
            spawn100TextGO.transform.SetParent(spawn100BtnGO.transform, false);
            var spawn100TextRect = spawn100TextGO.AddComponent<RectTransform>();
            spawn100TextRect.anchorMin = Vector2.zero;
            spawn100TextRect.anchorMax = Vector2.one;
            spawn100TextRect.sizeDelta = Vector2.zero;

            var spawn100Text = spawn100TextGO.AddComponent<Text>();
            spawn100Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            spawn100Text.fontSize = 12;
            spawn100Text.color = Color.white;
            spawn100Text.alignment = TextAnchor.MiddleCenter;
            spawn100Text.text = "+100 клиентов";

            yPos -= 25;

            // Кнопка "Показать клиентов"
            var showBtnGO = new GameObject("Btn_ShowClients");
            showBtnGO.transform.SetParent(panel.transform, false);
            var showBtnRect = showBtnGO.AddComponent<RectTransform>();
            showBtnRect.anchorMin = new Vector2(0, 1);
            showBtnRect.anchorMax = new Vector2(1, 1);
            showBtnRect.pivot = new Vector2(0.5f, 1);
            showBtnRect.anchoredPosition = new Vector2(0, yPos);
            showBtnRect.sizeDelta = new Vector2(-20, 22);

            var showBtnImage = showBtnGO.AddComponent<Image>();
            showBtnImage.color = new Color(0.4f, 0.2f, 0.2f, 1f);

            var showBtn = showBtnGO.AddComponent<Button>();
            showBtn.onClick.AddListener(() => debugMenu.ShowActiveClients());

            var showTextGO = new GameObject("Text");
            showTextGO.transform.SetParent(showBtnGO.transform, false);
            var showTextRect = showTextGO.AddComponent<RectTransform>();
            showTextRect.anchorMin = Vector2.zero;
            showTextRect.anchorMax = Vector2.one;
            showTextRect.sizeDelta = Vector2.zero;

            var showText = showTextGO.AddComponent<Text>();
            showText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            showText.fontSize = 12;
            showText.color = Color.white;
            showText.alignment = TextAnchor.MiddleCenter;
            showText.text = "Показать клиентов";
        }
    }
}
