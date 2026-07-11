// Assets/Editor/ArcJsonImporterWindow.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StorySystem.EditorTools;
using UnityEditor;
using UnityEngine;

namespace StorySystem.EditorTools
{
    /// <summary>
    /// Окно импорта сюжетных арок из JSON.
    /// Использование: Tools → AI Toolset → Import Story Arcs.
    ///
    /// Большой JSON (>~64 KB) не отображается в TextArea напрямую,
    /// потому что это вызывает известный native-crash в Unity 6 IMGUI
    /// (PPtr&lt;Shader&gt;::GetShader в GUIStyle::RenderText). Вместо этого
    /// окно хранит JSON в обычном строковом поле и показывает краткую
    /// статистику; для просмотра/правки открывается файл блокнотом ОС.
    /// </summary>
    public class ArcJsonImporterWindow : EditorWindow
    {
        string _jsonInput = "";
        string _arcFolder = "Assets/Resources/Arcs";
        bool _overwrite = true;
        bool _autoImportOnPaste = true; // ← Q2
        string _lastReport = "";
        string _statusLine = "JSON не загружен.";

        // ← Q1: список последних импортированных ассетов для подсветки в Project
        readonly List<string> _lastImportedAssetPaths = new List<string>();
        string _lastImportedRootFolder = "";

        Vector2 _scroll;

        [MenuItem("Tools/AI Toolset/Import Story Arcs")]
        public static void ShowWindow() => GetWindow<ArcJsonImporterWindow>("Story Arcs Importer");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Сюжетные арки: импорт из JSON", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Формат: массив объектов ArcDefinition. Каждая арка содержит массив stages, " +
                "в каждом — dialogueStructure с nodes. Связи между нодами — по стабильным nodeID.\n\n" +
                "Перед импортом убедитесь, что:\n" +
                "  • Архетипы в JSON совпадают с существующими в ArchetypeDatabase.\n" +
                "  • Значения Emotion и ClientGoal — допустимые в проекте.",
                MessageType.Info);

            _arcFolder = EditorGUILayout.TextField("Arc Asset Folder", _arcFolder);
            _overwrite = EditorGUILayout.Toggle("Overwrite Existing", _overwrite);
            _autoImportOnPaste = EditorGUILayout.Toggle(
                new GUIContent("Auto-import on Paste",
                    "При включении кнопка «Paste from Clipboard» сразу запускает импорт, без диалога подтверждения. " +
                    "Требуется валидный JSON в буфере обмена."),
                _autoImportOnPaste);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("JSON", EditorStyles.boldLabel);

            // Поле статуса — НЕ показываем сырой JSON крупным TextArea (вызывает crash).
            EditorGUILayout.LabelField(_statusLine, EditorStyles.miniLabel, GUILayout.MinHeight(20));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load from file…"))
                {
                    string p = EditorUtility.OpenFilePanel("Open story arcs JSON", Application.dataPath, "json");
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    {
                        LoadJsonIntoField(File.ReadAllText(p));
                        ShowNotification(new GUIContent("JSON загружен"));
                    }
                }
                if (GUILayout.Button("Paste from Clipboard"))
                {
                    string cb = EditorGUIUtility.systemCopyBuffer;
                    if (string.IsNullOrEmpty(cb))
                    {
                        EditorUtility.DisplayDialog("Story Arcs", "Буфер обмена пуст.", "OK");
                    }
                    else
                    {
                        LoadJsonIntoField(cb);
                        ShowNotification(new GUIContent("JSON вставлен из буфера"));

                        // ← Q2: при включённом Auto-import сразу запускаем импорт.
                        if (_autoImportOnPaste)
                        {
                            DoImport(showDialog: false, autoTriggered: true);
                        }
                    }
                }
                if (GUILayout.Button("Save JSON to File…"))
                {
                    if (!string.IsNullOrEmpty(_jsonInput))
                    {
                        string defaultName = "story_arcs.json";
                        string p = EditorUtility.SaveFilePanel("Save story arcs JSON", Application.dataPath, defaultName, "json");
                        if (!string.IsNullOrEmpty(p))
                        {
                            File.WriteAllText(p, _jsonInput);
                            if (p.StartsWith(Application.dataPath))
                            {
                                AssetDatabase.Refresh();
                                var obj = AssetDatabase.LoadAssetAtPath<Object>("Assets" + p.Substring(Application.dataPath.Length));
                                if (obj != null) EditorGUIUtility.PingObject(obj);
                            }
                            ShowNotification(new GUIContent("Сохранено"));
                        }
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Story Arcs", "Нет JSON для сохранения.", "OK");
                    }
                }
                if (GUILayout.Button("Open in Notepad"))
                {
                    if (string.IsNullOrEmpty(_jsonInput))
                    {
                        EditorUtility.DisplayDialog("Story Arcs", "Нет JSON для просмотра.", "OK");
                    }
                    else
                    {
                        string path = Path.Combine(Path.GetTempPath(), "story_arcs_edit.json");
                        File.WriteAllText(path, _jsonInput);
                        System.Diagnostics.Process.Start("notepad.exe", path);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_jsonInput)))
                {
                    if (GUILayout.Button("Validate"))
                    {
                        var report = ArcJsonImporter.ValidateJson(_jsonInput);
                        _lastReport = report.ToString();
                        EditorUtility.DisplayDialog("Story Arcs (Validate)", _lastReport, "OK");
                    }
                    if (GUILayout.Button("Import", GUILayout.Height(28)))
                    {
                        DoImport(showDialog: true, autoTriggered: false);
                    }
                }
            }

            // ← Q1: после успешного импорта показываем ссылку на созданную папку.
            if (_lastImportedAssetPaths.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Последний импорт:", EditorStyles.boldLabel);

                if (!string.IsNullOrEmpty(_lastImportedRootFolder))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Папка:", _lastImportedRootFolder, EditorStyles.miniLabel);
                        if (GUILayout.Button("Ping Folder", GUILayout.MaxWidth(110)))
                        {
                            PingAssetByPath(_lastImportedRootFolder);
                        }
                        if (GUILayout.Button("Reveal in OS", GUILayout.MaxWidth(110)))
                        {
                            RevealInOS(_lastImportedRootFolder);
                        }
                    }
                }

                int shown = 0;
                foreach (var path in _lastImportedAssetPaths)
                {
                    if (shown >= 12) break;
                    if (string.IsNullOrEmpty(path)) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                        if (GUILayout.Button("Ping", GUILayout.MaxWidth(50)))
                        {
                            PingAssetByPath(path);
                        }
                    }
                    shown++;
                }
                if (_lastImportedAssetPaths.Count > shown)
                    EditorGUILayout.LabelField($"…и ещё {_lastImportedAssetPaths.Count - shown} ассетов", EditorStyles.miniLabel);
            }

            if (!string.IsNullOrEmpty(_lastReport))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Last report:", EditorStyles.miniBoldLabel);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(100));
                EditorGUILayout.SelectableLabel(_lastReport, EditorStyles.textArea, GUILayout.Height(80));
                EditorGUILayout.EndScrollView();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Copy report to Clipboard"))
                    {
                        EditorGUIUtility.systemCopyBuffer = _lastReport;
                        ShowNotification(new GUIContent("Отчёт скопирован"));
                    }
                    if (GUILayout.Button("Clear last import list"))
                    {
                        _lastImportedAssetPaths.Clear();
                        _lastImportedRootFolder = null;
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Совет: для редактирования большого JSON используйте кнопку «Open in Notepad».",
                EditorStyles.wordWrappedMiniLabel);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        void LoadJsonIntoField(string json)
        {
            _jsonInput = json ?? string.Empty;
            UpdateStatus();
        }

        void UpdateStatus()
        {
            if (string.IsNullOrEmpty(_jsonInput))
            {
                _statusLine = "JSON не загружен.";
                return;
            }

            int len = _jsonInput.Length;
            int arcs = CountOccurrences(_jsonInput, "\"arcID\"");
            int stages = CountOccurrences(_jsonInput, "\"dayOffset\"");
            int dialogues = CountOccurrences(_jsonInput, "\"dialogueStructure\"");
            _statusLine = $"Загружено: {len:N0} символов · арок: {arcs} · этапов: {stages} · диалогов: {dialogues} · первая строка: '{SafeFirstLine(_jsonInput)}'";
        }

        void OnEnable()
        {
            UpdateStatus();
        }

        void DoImport(bool showDialog, bool autoTriggered)
        {
            if (string.IsNullOrEmpty(_jsonInput))
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("Story Arcs", "Введите JSON или загрузите файл.", "OK");
                return;
            }

            try
            {
                _lastImportedAssetPaths.Clear();
                _lastImportedRootFolder = null;

                var report = ArcJsonImporter.ImportJson(_jsonInput, _arcFolder, _overwrite);
                _lastReport = report.ToString();

                // Собираем список созданных ассетов и корневую папку арок.
                string trimmedFolder = _arcFolder.TrimEnd('/');
                foreach (var w in report.warnings)
                {
                    // warning-формат импортёра содержит "arcID"; берём все предупреждения по arcID.
                    // Корневую папку получим ниже из реального файл-скана.
                }
                _lastImportedRootFolder = trimmedFolder;

                // Сканируем реально созданные арк-папки по arcID из JSON, чтобы
                // построить список валидных путей для Ping.
                var arcIds = ExtractArcIds(_jsonInput);
                foreach (var arcId in arcIds)
                {
                    string arcFolder = $"{trimmedFolder}/{arcId}";
                    if (AssetDatabase.IsValidFolder(arcFolder))
                    {
                        string main = $"{arcFolder}/{arcId}.asset";
                        if (AssetDatabase.LoadAssetAtPath<Object>(main) != null)
                            _lastImportedAssetPaths.Add(main);

                        string dialoguesFolder = $"{arcFolder}/Dialogues";
                        if (AssetDatabase.IsValidFolder(dialoguesFolder))
                        {
                            string[] guids = AssetDatabase.FindAssets("t:DialogueGraph", new[] { dialoguesFolder });
                            foreach (var g in guids)
                            {
                                string path = AssetDatabase.GUIDToAssetPath(g);
                                if (!string.IsNullOrEmpty(path))
                                    _lastImportedAssetPaths.Add(path);
                            }
                        }
                    }
                }

                // ← Q1: подсветить созданную папку арок в окне Project.
                PingAssetByPath(_lastImportedRootFolder);

                if (showDialog)
                    EditorUtility.DisplayDialog("Story Arcs", _lastReport, "OK");
                else if (autoTriggered)
                    ShowNotification(new GUIContent("Импорт завершён"));
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                if (showDialog)
                    EditorUtility.DisplayDialog("Story Arcs", "Ошибка импорта:\n" + ex.Message, "OK");
                ShowNotification(new GUIContent("Ошибка импорта"));
            }
        }

        static void PingAssetByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj == null) return;
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }

        static void RevealInOS(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (Directory.Exists(path))
            {
                EditorUtility.RevealInFinder(path);
            }
        }

        static IEnumerable<string> ExtractArcIds(string json)
        {
            if (string.IsNullOrEmpty(json)) yield break;
            const string token = "\"arcID\"";
            int idx = 0;
            while ((idx = json.IndexOf(token, idx, System.StringComparison.Ordinal)) != -1)
            {
                int colon = json.IndexOf(':', idx + token.Length);
                if (colon < 0) break;
                int q1 = json.IndexOf('"', colon + 1);
                if (q1 < 0) break;
                int q2 = json.IndexOf('"', q1 + 1);
                if (q2 < 0) break;
                string id = json.Substring(q1 + 1, q2 - q1 - 1);
                if (!string.IsNullOrEmpty(id))
                    yield return id;
                idx = q2 + 1;
            }
        }

        static int CountOccurrences(string source, string token)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(token)) return 0;
            int count = 0;
            int idx = 0;
            while ((idx = source.IndexOf(token, idx, System.StringComparison.Ordinal)) != -1)
            {
                count++;
                idx += token.Length;
            }
            return count;
        }

        static string SafeFirstLine(string source)
        {
            if (string.IsNullOrEmpty(source)) return "";
            int newline = source.IndexOf('\n');
            string first = newline >= 0 ? source.Substring(0, newline) : source;
            if (first.Length > 80) first = first.Substring(0, 80) + "…";
            return first.Trim();
        }
    }
}