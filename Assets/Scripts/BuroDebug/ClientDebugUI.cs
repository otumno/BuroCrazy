using System.Collections.Generic;
using Data;
using UnityEngine;
using UnityEngine.UI;

namespace BuroDebug
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
            panelRect.sizeDelta = new Vector2(250, 430);

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

            // === КНОПКИ ДЛЯ ПОЖИЛЫХ (ELDERLY) ===
            // Кнопка "Спавн пожилого 1"
            var spawnElderly1BtnGO = new GameObject("Btn_Elderly_1");
            spawnElderly1BtnGO.transform.SetParent(panel.transform, false);
            var spawnElderly1BtnRect = spawnElderly1BtnGO.AddComponent<RectTransform>();
            spawnElderly1BtnRect.anchorMin = new Vector2(0, 1);
            spawnElderly1BtnRect.anchorMax = new Vector2(1, 1);
            spawnElderly1BtnRect.pivot = new Vector2(0.5f, 1);
            spawnElderly1BtnRect.anchoredPosition = new Vector2(0, yPos);
            spawnElderly1BtnRect.sizeDelta = new Vector2(-20, 22);

            var spawnElderly1BtnImage = spawnElderly1BtnGO.AddComponent<Image>();
            spawnElderly1BtnImage.color = new Color(0.5f, 0.3f, 0.6f, 1f);

            var spawnElderly1Btn = spawnElderly1BtnGO.AddComponent<Button>();
            spawnElderly1Btn.onClick.AddListener(() => debugMenu.SpawnElderly());

            var spawnElderly1TextGO = new GameObject("Text");
            spawnElderly1TextGO.transform.SetParent(spawnElderly1BtnGO.transform, false);
            var spawnElderly1TextRect = spawnElderly1TextGO.AddComponent<RectTransform>();
            spawnElderly1TextRect.anchorMin = Vector2.zero;
            spawnElderly1TextRect.anchorMax = Vector2.one;
            spawnElderly1TextRect.sizeDelta = Vector2.zero;

            var spawnElderly1Text = spawnElderly1TextGO.AddComponent<Text>();
            spawnElderly1Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            spawnElderly1Text.fontSize = 12;
            spawnElderly1Text.color = Color.white;
            spawnElderly1Text.alignment = TextAnchor.MiddleCenter;
            spawnElderly1Text.text = "Спавн пожилого 1";

            yPos -= 25;

            // Кнопка "Спавн пожилого 10"
            var spawnElderly10BtnGO = new GameObject("Btn_Elderly_10");
            spawnElderly10BtnGO.transform.SetParent(panel.transform, false);
            var spawnElderly10BtnRect = spawnElderly10BtnGO.AddComponent<RectTransform>();
            spawnElderly10BtnRect.anchorMin = new Vector2(0, 1);
            spawnElderly10BtnRect.anchorMax = new Vector2(1, 1);
            spawnElderly10BtnRect.pivot = new Vector2(0.5f, 1);
            spawnElderly10BtnRect.anchoredPosition = new Vector2(0, yPos);
            spawnElderly10BtnRect.sizeDelta = new Vector2(-20, 22);

            var spawnElderly10BtnImage = spawnElderly10BtnGO.AddComponent<Image>();
            spawnElderly10BtnImage.color = new Color(0.4f, 0.25f, 0.5f, 1f);

            var spawnElderly10Btn = spawnElderly10BtnGO.AddComponent<Button>();
            spawnElderly10Btn.onClick.AddListener(() => debugMenu.SpawnElderlyMultiple(10));

            var spawnElderly10TextGO = new GameObject("Text");
            spawnElderly10TextGO.transform.SetParent(spawnElderly10BtnGO.transform, false);
            var spawnElderly10TextRect = spawnElderly10TextGO.AddComponent<RectTransform>();
            spawnElderly10TextRect.anchorMin = Vector2.zero;
            spawnElderly10TextRect.anchorMax = Vector2.one;
            spawnElderly10TextRect.sizeDelta = Vector2.zero;

            var spawnElderly10Text = spawnElderly10TextGO.AddComponent<Text>();
            spawnElderly10Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            spawnElderly10Text.fontSize = 12;
            spawnElderly10Text.color = Color.white;
            spawnElderly10Text.alignment = TextAnchor.MiddleCenter;
            spawnElderly10Text.text = "Спавн пожилого 10";

            yPos -= 25;

            // Кнопка "Спавн пожилого 100"
            var spawnElderly100BtnGO = new GameObject("Btn_Elderly_100");
            spawnElderly100BtnGO.transform.SetParent(panel.transform, false);
            var spawnElderly100BtnRect = spawnElderly100BtnGO.AddComponent<RectTransform>();
            spawnElderly100BtnRect.anchorMin = new Vector2(0, 1);
            spawnElderly100BtnRect.anchorMax = new Vector2(1, 1);
            spawnElderly100BtnRect.pivot = new Vector2(0.5f, 1);
            spawnElderly100BtnRect.anchoredPosition = new Vector2(0, yPos);
            spawnElderly100BtnRect.sizeDelta = new Vector2(-20, 22);

            var spawnElderly100BtnImage = spawnElderly100BtnGO.AddComponent<Image>();
            spawnElderly100BtnImage.color = new Color(0.3f, 0.2f, 0.4f, 1f);

            var spawnElderly100Btn = spawnElderly100BtnGO.AddComponent<Button>();
            spawnElderly100Btn.onClick.AddListener(() => debugMenu.SpawnElderlyMultiple(100));

            var spawnElderly100TextGO = new GameObject("Text");
            spawnElderly100TextGO.transform.SetParent(spawnElderly100BtnGO.transform, false);
            var spawnElderly100TextRect = spawnElderly100TextGO.AddComponent<RectTransform>();
            spawnElderly100TextRect.anchorMin = Vector2.zero;
            spawnElderly100TextRect.anchorMax = Vector2.one;
            spawnElderly100TextRect.sizeDelta = Vector2.zero;

            var spawnElderly100Text = spawnElderly100TextGO.AddComponent<Text>();
            spawnElderly100Text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            spawnElderly100Text.fontSize = 12;
            spawnElderly100Text.color = Color.white;
            spawnElderly100Text.alignment = TextAnchor.MiddleCenter;
            spawnElderly100Text.text = "Спавн пожилого 100";

            yPos -= 25;

            // Кнопка "Спавн пожилого (К Директору)"
            var spawnElderlyDirBtnGO = new GameObject("Btn_Elderly_Director");
            spawnElderlyDirBtnGO.transform.SetParent(panel.transform, false);
            var spawnElderlyDirBtnRect = spawnElderlyDirBtnGO.AddComponent<RectTransform>();
            spawnElderlyDirBtnRect.anchorMin = new Vector2(0, 1);
            spawnElderlyDirBtnRect.anchorMax = new Vector2(1, 1);
            spawnElderlyDirBtnRect.pivot = new Vector2(0.5f, 1);
            spawnElderlyDirBtnRect.anchoredPosition = new Vector2(0, yPos);
            spawnElderlyDirBtnRect.sizeDelta = new Vector2(-20, 22);

            var spawnElderlyDirBtnImage = spawnElderlyDirBtnGO.AddComponent<Image>();
            spawnElderlyDirBtnImage.color = new Color(0.6f, 0.2f, 0.2f, 1f);

            var spawnElderlyDirBtn = spawnElderlyDirBtnGO.AddComponent<Button>();
            spawnElderlyDirBtn.onClick.AddListener(() => debugMenu.SpawnElderlyForDirector());

            var spawnElderlyDirTextGO = new GameObject("Text");
            spawnElderlyDirTextGO.transform.SetParent(spawnElderlyDirBtnGO.transform, false);
            var spawnElderlyDirTextRect = spawnElderlyDirTextGO.AddComponent<RectTransform>();
            spawnElderlyDirTextRect.anchorMin = Vector2.zero;
            spawnElderlyDirTextRect.anchorMax = Vector2.one;
            spawnElderlyDirTextRect.sizeDelta = Vector2.zero;

            var spawnElderlyDirText = spawnElderlyDirTextGO.AddComponent<Text>();
            spawnElderlyDirText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            spawnElderlyDirText.fontSize = 12;
            spawnElderlyDirText.color = Color.white;
            spawnElderlyDirText.alignment = TextAnchor.MiddleCenter;
            spawnElderlyDirText.text = "Бабушка (К Директору)";

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

            yPos -= 25;

            // Кнопка "Сюжетный гость №1"
            var specialVisitorBtnGO = new GameObject("Btn_SpecialVisitor");
            specialVisitorBtnGO.transform.SetParent(panel.transform, false);
            var specialVisitorBtnRect = specialVisitorBtnGO.AddComponent<RectTransform>();
            specialVisitorBtnRect.anchorMin = new Vector2(0, 1);
            specialVisitorBtnRect.anchorMax = new Vector2(1, 1);
            specialVisitorBtnRect.pivot = new Vector2(0.5f, 1);
            specialVisitorBtnRect.anchoredPosition = new Vector2(0, yPos);
            specialVisitorBtnRect.sizeDelta = new Vector2(-20, 22);

            var specialVisitorBtnImage = specialVisitorBtnGO.AddComponent<Image>();
            specialVisitorBtnImage.color = new Color(0.8f, 0.4f, 0.2f, 1f);

            var specialVisitorBtn = specialVisitorBtnGO.AddComponent<Button>();
            specialVisitorBtn.onClick.AddListener(() => debugMenu.SpawnFirstSpecialVisitor());

            var specialVisitorTextGO = new GameObject("Text");
            specialVisitorTextGO.transform.SetParent(specialVisitorBtnGO.transform, false);
            var specialVisitorTextRect = specialVisitorTextGO.AddComponent<RectTransform>();
            specialVisitorTextRect.anchorMin = Vector2.zero;
            specialVisitorTextRect.anchorMax = Vector2.one;
            specialVisitorTextRect.sizeDelta = Vector2.zero;

            var specialVisitorText = specialVisitorTextGO.AddComponent<Text>();
            specialVisitorText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            specialVisitorText.fontSize = 12;
            specialVisitorText.color = Color.white;
            specialVisitorText.alignment = TextAnchor.MiddleCenter;
            specialVisitorText.text = "Сюжетный гость №1";
        }
    }
}
