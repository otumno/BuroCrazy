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

            // Панель меню (основа для ScrollView и infoText)
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

            // Info текст (ВНЕ ScrollView, всегда виден)
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

            // ScrollView -> Viewport -> Content
            var scrollViewGO = new GameObject("ScrollView");
            scrollViewGO.transform.SetParent(panel.transform, false);
            var scrollViewRect = scrollViewGO.AddComponent<RectTransform>();
            scrollViewRect.anchorMin = new Vector2(0, 0);
            scrollViewRect.anchorMax = new Vector2(1, 1);
            scrollViewRect.pivot = new Vector2(0.5f, 1);
            scrollViewRect.anchoredPosition = new Vector2(0, -130);
            scrollViewRect.sizeDelta = new Vector2(-10, -140);

            var scrollRect = scrollViewGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollViewGO.transform, false);
            var viewportRect = viewportGO.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = Vector2.up;
            viewportRect.sizeDelta = Vector2.zero;

            var viewportMask = viewportGO.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            var viewportImage = viewportGO.AddComponent<Image>();
            viewportImage.color = Color.white;

            scrollRect.viewport = viewportRect;

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(-10, 0);
            contentRect.anchoredPosition = new Vector2(0, 0);

            var verticalLayout = contentGO.AddComponent<VerticalLayoutGroup>();
            verticalLayout.padding = new RectOffset(5, 5, 5, 5);
            verticalLayout.spacing = 3;
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            var contentFitter = contentGO.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;

            // Кнопки для групп - теперь под Content
            CreateGroupButtons(contentRect);

            panel.SetActive(false);
        }

        private void CreateGroupButtons(Transform parent)
        {
            // === КНОПКА "АНЛОК ВСЕХ РЕГИОНОВ" (в самом верху, повыше) ===
            var unlockAllBtnGO = new GameObject("Btn_UnlockAllRegions");
            unlockAllBtnGO.transform.SetParent(parent, false);
            var unlockAllBtnRect = unlockAllBtnGO.AddComponent<RectTransform>();
            unlockAllBtnRect.anchorMin = new Vector2(0, 1);
            unlockAllBtnRect.anchorMax = new Vector2(1, 1);
            unlockAllBtnRect.pivot = new Vector2(0.5f, 1);
            unlockAllBtnRect.anchoredPosition = Vector2.zero;
            unlockAllBtnRect.sizeDelta = new Vector2(-10, 26);

            var unlockAllBtnImage = unlockAllBtnGO.AddComponent<Image>();
            unlockAllBtnImage.color = new Color(0.2f, 0.8f, 0.2f, 1f); // зелёный

            var unlockAllBtn = unlockAllBtnGO.AddComponent<Button>();
            unlockAllBtn.onClick.AddListener(() => debugMenu.UnlockAllRegions());

            var unlockAllTextGO = new GameObject("Text");
            unlockAllTextGO.transform.SetParent(unlockAllBtnGO.transform, false);
            var unlockAllTextRect = unlockAllTextGO.AddComponent<RectTransform>();
            unlockAllTextRect.anchorMin = Vector2.zero;
            unlockAllTextRect.anchorMax = Vector2.one;
            unlockAllTextRect.sizeDelta = Vector2.zero;

            var unlockAllText = unlockAllTextGO.AddComponent<Text>();
            unlockAllText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            unlockAllText.fontSize = 12;
            unlockAllText.color = Color.white;
            unlockAllText.alignment = TextAnchor.MiddleCenter;
            unlockAllText.text = "Анлок всех регионов";

            // === АВТО-КНОПКИ ДЛЯ ГРУПП ===
            var db = Resources.Load<ArchetypeDatabase>("Databases/ArchetypeDatabase");
            if (db != null && db.allArchetypes != null)
            {
                var groups = new HashSet<string>();
                foreach (var a in db.allArchetypes)
                {
                    if (a != null) groups.Add(a.groupID);
                }

                foreach (var group in groups)
                {
                    var btnGO = new GameObject($"Btn_{group}");
                    btnGO.transform.SetParent(parent, false);

                    var btnRect = btnGO.AddComponent<RectTransform>();
                    btnRect.anchorMin = new Vector2(0, 1);
                    btnRect.anchorMax = new Vector2(1, 1);
                    btnRect.pivot = new Vector2(0.5f, 1);
                    btnRect.anchoredPosition = Vector2.zero;
                    btnRect.sizeDelta = new Vector2(-10, 18);

                    var btnImage = btnGO.AddComponent<Image>();
                    btnImage.color = new Color(0.2f, 0.4f, 0.8f, 1f);

                    var btn = btnGO.AddComponent<Button>();
                    btn.onClick.AddListener(() => debugMenu.SpawnByGroup(group));

                    var textGO = new GameObject("Text");
                    textGO.transform.SetParent(btnGO.transform, false);
                    var textRect = textGO.AddComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.sizeDelta = Vector2.zero;

                    var text = textGO.AddComponent<Text>();
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.fontSize = 11;
                    text.color = Color.white;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.text = $"Spawn {group}";
                }
            }

            // === КНОПКА "SPAWN ELDERLY" (одна вместо нескольких) ===
            var spawnElderlyBtnGO = new GameObject("Btn_Elderly");
            spawnElderlyBtnGO.transform.SetParent(parent, false);
            var spawnElderlyBtnRect = spawnElderlyBtnGO.AddComponent<RectTransform>();
            spawnElderlyBtnRect.anchorMin = new Vector2(0, 1);
            spawnElderlyBtnRect.anchorMax = new Vector2(1, 1);
            spawnElderlyBtnRect.pivot = new Vector2(0.5f, 1);
            spawnElderlyBtnRect.anchoredPosition = Vector2.zero;
            spawnElderlyBtnRect.sizeDelta = new Vector2(-10, 20);

            var spawnElderlyBtnImage = spawnElderlyBtnGO.AddComponent<Image>();
            spawnElderlyBtnImage.color = new Color(0.5f, 0.3f, 0.6f, 1f);

            var spawnElderlyBtn = spawnElderlyBtnGO.AddComponent<Button>();
            spawnElderlyBtn.onClick.AddListener(() => debugMenu.SpawnElderly());

            var spawnElderlyTextGO = new GameObject("Text");
            spawnElderlyTextGO.transform.SetParent(spawnElderlyBtnGO.transform, false);
            var spawnElderlyTextRect = spawnElderlyTextGO.AddComponent<RectTransform>();
            spawnElderlyTextRect.anchorMin = Vector2.zero;
            spawnElderlyTextRect.anchorMax = Vector2.one;
            spawnElderlyTextRect.sizeDelta = Vector2.zero;

            var spawnElderlyText = spawnElderlyTextGO.AddComponent<Text>();
            spawnElderlyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            spawnElderlyText.fontSize = 11;
            spawnElderlyText.color = Color.white;
            spawnElderlyText.alignment = TextAnchor.MiddleCenter;
            spawnElderlyText.text = "Spawn Elderly";

            // === КНОПКА "+10 КЛИЕНТОВ" ===
            var spawn10BtnGO = new GameObject("Btn_Spawn10");
            spawn10BtnGO.transform.SetParent(parent, false);
            var spawn10BtnRect = spawn10BtnGO.AddComponent<RectTransform>();
            spawn10BtnRect.anchorMin = new Vector2(0, 1);
            spawn10BtnRect.anchorMax = new Vector2(1, 1);
            spawn10BtnRect.pivot = new Vector2(0.5f, 1);
            spawn10BtnRect.anchoredPosition = Vector2.zero;
            spawn10BtnRect.sizeDelta = new Vector2(-10, 20);

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
            spawn10Text.fontSize = 11;
            spawn10Text.color = Color.white;
            spawn10Text.alignment = TextAnchor.MiddleCenter;
            spawn10Text.text = "+10 клиентов";

            // === КНОПКА "ПОКАЗАТЬ КЛИЕНТОВ" ===
            var showBtnGO = new GameObject("Btn_ShowClients");
            showBtnGO.transform.SetParent(parent, false);
            var showBtnRect = showBtnGO.AddComponent<RectTransform>();
            showBtnRect.anchorMin = new Vector2(0, 1);
            showBtnRect.anchorMax = new Vector2(1, 1);
            showBtnRect.pivot = new Vector2(0.5f, 1);
            showBtnRect.anchoredPosition = Vector2.zero;
            showBtnRect.sizeDelta = new Vector2(-10, 20);

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
            showText.fontSize = 11;
            showText.color = Color.white;
            showText.alignment = TextAnchor.MiddleCenter;
            showText.text = "Показать клиентов";

            // === КНОПКА "СЮЖЕТНЫЙ ГОСТЬ №1" ===
            var specialVisitorBtnGO = new GameObject("Btn_SpecialVisitor");
            specialVisitorBtnGO.transform.SetParent(parent, false);
            var specialVisitorBtnRect = specialVisitorBtnGO.AddComponent<RectTransform>();
            specialVisitorBtnRect.anchorMin = new Vector2(0, 1);
            specialVisitorBtnRect.anchorMax = new Vector2(1, 1);
            specialVisitorBtnRect.pivot = new Vector2(0.5f, 1);
            specialVisitorBtnRect.anchoredPosition = Vector2.zero;
            specialVisitorBtnRect.sizeDelta = new Vector2(-10, 20);

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
            specialVisitorText.fontSize = 11;
            specialVisitorText.color = Color.white;
            specialVisitorText.alignment = TextAnchor.MiddleCenter;
            specialVisitorText.text = "Сюжетный гость №1";

            // === КНОПКА "ТЕСТ: КЛОУН" ===
            var testClownBtnGO = new GameObject("Btn_TestClown");
            testClownBtnGO.transform.SetParent(parent, false);
            var testClownBtnRect = testClownBtnGO.AddComponent<RectTransform>();
            testClownBtnRect.anchorMin = new Vector2(0, 1);
            testClownBtnRect.anchorMax = new Vector2(1, 1);
            testClownBtnRect.pivot = new Vector2(0.5f, 1);
            testClownBtnRect.anchoredPosition = Vector2.zero;
            testClownBtnRect.sizeDelta = new Vector2(-10, 20);

            var testClownBtnImage = testClownBtnGO.AddComponent<Image>();
            testClownBtnImage.color = new Color(0.8f, 0.4f, 0.8f, 1f); // фиолетовый

            var testClownBtn = testClownBtnGO.AddComponent<Button>();
            testClownBtn.onClick.AddListener(() => debugMenu.SpawnTestClown());

            var testClownTextGO = new GameObject("Text");
            testClownTextGO.transform.SetParent(testClownBtnGO.transform, false);
            var testClownTextRect = testClownTextGO.AddComponent<RectTransform>();
            testClownTextRect.anchorMin = Vector2.zero;
            testClownTextRect.anchorMax = Vector2.one;
            testClownTextRect.sizeDelta = Vector2.zero;

            var testClownText = testClownTextGO.AddComponent<Text>();
            testClownText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            testClownText.fontSize = 11;
            testClownText.color = Color.white;
            testClownText.alignment = TextAnchor.MiddleCenter;
            testClownText.text = "Тест: Клоун";

            // === КНОПКА "ТЕСТ: УБОРЩИКИ" ===
            var testCleanersBtnGO = new GameObject("Btn_TestCleaners");
            testCleanersBtnGO.transform.SetParent(parent, false);
            var testCleanersBtnRect = testCleanersBtnGO.AddComponent<RectTransform>();
            testCleanersBtnRect.anchorMin = new Vector2(0, 1);
            testCleanersBtnRect.anchorMax = new Vector2(1, 1);
            testCleanersBtnRect.pivot = new Vector2(0.5f, 1);
            testCleanersBtnRect.anchoredPosition = Vector2.zero;
            testCleanersBtnRect.sizeDelta = new Vector2(-10, 20);

            var testCleanersBtnImage = testCleanersBtnGO.AddComponent<Image>();
            testCleanersBtnImage.color = new Color(0.4f, 0.8f, 0.8f, 1f); // бирюзовый

            var testCleanersBtn = testCleanersBtnGO.AddComponent<Button>();
            testCleanersBtn.onClick.AddListener(() => debugMenu.SpawnTestCleaners());

            var testCleanersTextGO = new GameObject("Text");
            testCleanersTextGO.transform.SetParent(testCleanersBtnGO.transform, false);
            var testCleanersTextRect = testCleanersTextGO.AddComponent<RectTransform>();
            testCleanersTextRect.anchorMin = Vector2.zero;
            testCleanersTextRect.anchorMax = Vector2.one;
            testCleanersTextRect.sizeDelta = Vector2.zero;

            var testCleanersText = testCleanersTextGO.AddComponent<Text>();
            testCleanersText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            testCleanersText.fontSize = 11;
            testCleanersText.color = Color.white;
            testCleanersText.alignment = TextAnchor.MiddleCenter;
            testCleanersText.text = "Тест: Уборщики";
        }
    }
}
