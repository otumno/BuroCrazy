using StaffOrders;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace UI.StaffOrders.Editor
{
    /// <summary>
    /// Собирает меню приказов по ПКМ: каталог приказов, префабы меню и пункта, экземпляр меню в открытой сцене.
    /// </summary>
    // Генератор, а не префаб, собранный руками: так меню можно пересобрать одной командой.
    // Повторный запуск перезаписывает префабы, но каталог не трогает, чтобы не стереть настройки геймдизайна.
    public static class StaffOrderMenuBuilder
    {
        private const string PrefabFolder = "Assets/Prefabs/UI/StaffOrders";
        private const string MenuPrefabPath = PrefabFolder + "/StaffOrderMenu.prefab";
        private const string ItemPrefabPath = PrefabFolder + "/StaffOrderMenuItem.prefab";
        private const string CatalogFolder = "Assets/Data/StaffOrders";
        private const string CatalogPath = CatalogFolder + "/StaffOrderCatalog.asset";
        private const string ActionsFolder = "Assets/Thoughts/StaffActions";
        private const string LogTag = "[StaffOrderMenuBuilder]";

        private const float PanelWidth = 280f;
        private const float ItemHeight = 30f;
        private const float HeaderHeight = 56f;
        private const float PortraitWidth = 44f;

        private static readonly Color PanelColor = new Color(0.16f, 0.14f, 0.12f, 0.96f);
        private static readonly Color HeaderColor = new Color(0.27f, 0.22f, 0.17f, 1f);
        private static readonly Color ItemColor = new Color(0.22f, 0.19f, 0.16f, 1f);
        private static readonly Color ItemHighlightColor = new Color(0.42f, 0.33f, 0.22f, 1f);
        private static readonly Color ItemPressedColor = new Color(0.14f, 0.12f, 0.10f, 1f);
        private static readonly Color TextColor = new Color(0.95f, 0.91f, 0.82f, 1f);
        private static readonly Color SubTextColor = new Color(0.76f, 0.70f, 0.60f, 1f);

        [MenuItem("Bureau/Create Staff Order Menu")]
        public static void CreateStaffOrderMenu()
        {
            var catalog = LoadOrCreateCatalog();
            var font = FindGameFont();

            EnsureFolder(PrefabFolder);
            var itemPrefab = BuildItemPrefab(font);
            var menuPrefab = BuildMenuPrefab(font, itemPrefab, catalog);
            AssetDatabase.SaveAssets();

            PlaceInOpenScene(menuPrefab);
        }

        // --- Каталог ---

        private static StaffOrderCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StaffOrderCatalog>(CatalogPath);
            if (catalog != null) return catalog;

            EnsureFolder(CatalogFolder);
            catalog = ScriptableObject.CreateInstance<StaffOrderCatalog>();

            // Общие приказы — для всех ролей.
            AddOrder(catalog, "Сделать паузу и отдохнуть", ActionsFolder + "/Action_GoToBreak.asset");
            AddOrder(catalog, "Сходить в туалет", ActionsFolder + "/Action_GoToToilet.asset");

            // Приказы по ролям.
            AddOrder(catalog, "Иди в патруль", ActionsFolder + "/Action_Guard_Patrol_NEW.asset",
                StaffController.Role.Guard);
            AddOrder(catalog, "Отнеси документы со стола в архив", ActionsFolder + "/TacticalAction/Action_TakeStackToArchive.asset",
                StaffController.Role.Clerk);
            AddOrder(catalog, "Пройдись и прибери", ActionsFolder + "/Action_JanitorPatrol.asset",
                StaffController.Role.Janitor, StaffController.Role.ServiceWorker);
            AddOrder(catalog, "Обойди офис", ActionsFolder + "/Action_InternPatrol.asset",
                StaffController.Role.Intern);

            AssetDatabase.CreateAsset(catalog, CatalogPath);
            Debug.Log($"{LogTag} Создан каталог приказов: {CatalogPath}");
            return catalog;
        }

        private static void AddOrder(StaffOrderCatalog catalog, string label, string actionPath, params StaffController.Role[] roles)
        {
            var action = AssetDatabase.LoadAssetAtPath<StaffAction>(actionPath);
            if (action == null)
            {
                Debug.LogWarning($"{LogTag} Не найдено действие {actionPath}, приказ «{label}» пропущен.");
                return;
            }

            catalog.Orders.Add(new StaffOrderDefinition(label, action, roles));
        }

        // --- Префабы ---

        private static StaffOrderMenuItemUI BuildItemPrefab(TMP_FontAsset font)
        {
            var root = CreateUIObject("StaffOrderMenuItem", null);

            // Фон белый, а цвета задаёт палитра кнопки: оттенок умножается на цвет картинки,
            // и подсветку светлее базового цвета иначе не получить (значения больше 1 обрезаются).
            var background = root.AddComponent<Image>();
            background.color = Color.white;

            var button = root.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.normalColor = ItemColor;
            colors.highlightedColor = ItemHighlightColor;
            colors.selectedColor = ItemColor;
            colors.pressedColor = ItemPressedColor;
            colors.disabledColor = new Color(ItemColor.r, ItemColor.g, ItemColor.b, 0.5f);
            button.colors = colors;

            var layout = root.AddComponent<LayoutElement>();
            layout.minHeight = ItemHeight;
            layout.preferredHeight = ItemHeight;

            var label = CreateText("Label", root.transform, font, 16f, TextColor, FontStyles.Normal);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(label.rectTransform, 10f, 2f);

            var item = root.AddComponent<StaffOrderMenuItemUI>();
            SetReference(item, "button", button);
            SetReference(item, "label", label);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ItemPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<StaffOrderMenuItemUI>();
        }

        private static GameObject BuildMenuPrefab(TMP_FontAsset font, StaffOrderMenuItemUI itemPrefab, StaffOrderCatalog catalog)
        {
            // Корень на весь экран — прозрачная подложка: клик мимо меню закрывает его.
            var root = CreateUIObject("StaffOrderMenu", null);
            Stretch((RectTransform)root.transform, 0f, 0f);
            var blocker = root.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            var backgroundButton = root.AddComponent<Button>();
            backgroundButton.transition = Selectable.Transition.None;
            backgroundButton.targetGraphic = blocker;

            var panel = CreateUIObject("Panel", root.transform);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = new Vector2(PanelWidth, 0f);
            panel.AddComponent<Image>().color = PanelColor;
            var panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(6, 6, 6, 6);
            panelLayout.spacing = 4f;
            SetChildControl(panelLayout);
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var portrait = BuildHeader(panel.transform, font, out var nameText, out var roleText);

            var items = CreateUIObject("Items", panel.transform);
            var itemsLayout = items.AddComponent<VerticalLayoutGroup>();
            itemsLayout.spacing = 3f;
            SetChildControl(itemsLayout);

            var menu = root.AddComponent<StaffOrderMenuUI>();
            SetReference(menu, "catalog", catalog);
            SetReference(menu, "portrait", portrait);
            SetReference(menu, "nameText", nameText);
            SetReference(menu, "roleText", roleText);
            SetReference(menu, "panel", panelRect);
            SetReference(menu, "itemsContainer", items.transform);
            SetReference(menu, "itemPrefab", itemPrefab);
            SetReference(menu, "backgroundButton", backgroundButton);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, MenuPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"{LogTag} Префаб меню сохранён: {MenuPrefabPath}");
            return prefab;
        }

        private static StaffPortraitUI BuildHeader(Transform parent, TMP_FontAsset font,
            out TextMeshProUGUI nameText, out TextMeshProUGUI roleText)
        {
            var header = CreateUIObject("Header", parent);
            header.AddComponent<Image>().color = HeaderColor;
            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(4, 6, 4, 4);
            headerLayout.spacing = 8f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = false;
            var headerElement = header.AddComponent<LayoutElement>();
            headerElement.minHeight = HeaderHeight;
            headerElement.preferredHeight = HeaderHeight;

            // Портрет: слои сотрудника друг на друге, как в карточке персонала.
            var portraitObject = CreateUIObject("Portrait", header.transform);
            var portraitElement = portraitObject.AddComponent<LayoutElement>();
            portraitElement.minWidth = PortraitWidth;
            portraitElement.preferredWidth = PortraitWidth;
            portraitElement.minHeight = HeaderHeight - 8f;
            portraitElement.preferredHeight = HeaderHeight - 8f;
            portraitObject.AddComponent<RectMask2D>();
            var portrait = portraitObject.AddComponent<StaffPortraitUI>();
            SetReference(portrait, "body", CreatePortraitLayer("Body", portraitObject.transform));
            SetReference(portrait, "outfit", CreatePortraitLayer("Outfit", portraitObject.transform));
            SetReference(portrait, "hair", CreatePortraitLayer("Hair", portraitObject.transform));
            SetReference(portrait, "face", CreatePortraitLayer("Face", portraitObject.transform));
            SetReference(portrait, "accessory", CreatePortraitLayer("Accessory", portraitObject.transform));

            var info = CreateUIObject("Info", header.transform);
            var infoLayout = info.AddComponent<VerticalLayoutGroup>();
            infoLayout.childAlignment = TextAnchor.MiddleLeft;
            SetChildControl(infoLayout);
            info.AddComponent<LayoutElement>().flexibleWidth = 1f;

            nameText = CreateText("Name", info.transform, font, 17f, TextColor, FontStyles.Bold);
            roleText = CreateText("Role", info.transform, font, 14f, SubTextColor, FontStyles.Italic);
            return portrait;
        }

        private static Image CreatePortraitLayer(string name, Transform parent)
        {
            var layer = CreateUIObject(name, parent);
            Stretch((RectTransform)layer.transform, 0f, 0f);
            var image = layer.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        // --- Сцена ---

        private static void PlaceInOpenScene(GameObject menuPrefab)
        {
            var existing = Object.FindFirstObjectByType<StaffOrderMenuUI>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log($"{LogTag} Меню уже есть в сцене ({existing.name}), обновлён только префаб.");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            // Ставим меню в тот же Canvas, где живут панели директора.
            var anchor = Object.FindFirstObjectByType<ActionConfigPopupUI>(FindObjectsInactive.Include);
            var canvas = anchor != null ? anchor.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Меню приказов",
                    "Префабы созданы, но в открытой сцене не найден игровой Canvas с окном тактики. " +
                    "Открой GameScene и запусти команду ещё раз.", "OK");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(menuPrefab, canvas.rootCanvas.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Create Staff Order Menu");
            instance.transform.SetAsLastSibling();
            Stretch((RectTransform)instance.transform, 0f, 0f);

            var menu = instance.GetComponent<StaffOrderMenuUI>();
            SetReference(menu, "tacticsPopup", anchor);

            var input = Object.FindFirstObjectByType<PlayerInputController>(FindObjectsInactive.Include);
            if (input != null)
            {
                Undo.RecordObject(input, "Assign Staff Order Menu");
                input.staffOrderMenu = menu;
                EditorUtility.SetDirty(input);
            }

            // Меню скрыто, пока его не откроет ПКМ.
            instance.SetActive(false);
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Selection.activeGameObject = instance;
            Debug.Log($"{LogTag} Меню приказов добавлено в сцену «{instance.scene.name}». Не забудь сохранить сцену.");
        }

        // --- Хелперы ---

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            if (parent != null) gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size, Color color, FontStyles style)
        {
            var text = CreateUIObject(name, parent).AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static void Stretch(RectTransform rect, float horizontalInset, float verticalInset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(horizontalInset, verticalInset);
            rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        }

        private static void SetChildControl(HorizontalOrVerticalLayoutGroup layout)
        {
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void SetReference(Object target, string fieldName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"{LogTag} У {target.GetType().Name} нет поля {fieldName}.");
                return;
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        // Шрифт берём у существующего игрового окна: дефолтный шрифт TMP может не содержать кириллицу.
        private static TMP_FontAsset FindGameFont()
        {
            var popup = Object.FindFirstObjectByType<ActionConfigPopupUI>(FindObjectsInactive.Include);
            var text = popup != null ? popup.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            return text != null && text.font != null ? text.font : TMP_Settings.defaultFontAsset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
