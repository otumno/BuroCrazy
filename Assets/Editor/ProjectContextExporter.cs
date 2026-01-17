using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ProjectContextExporter : EditorWindow
{
    // --- Настройки ---
    private bool includeTOC = true;
    private bool includeHierarchy = true;
    private bool expandPrefabsInHierarchy = true;
    private bool includeProjectMap = true;
    private bool includeScriptContents = true;
    
    // --- НОВАЯ НАСТРОЙКА ---
    private bool includeProjectAssets = true; // Включить структуру файлов проекта

    // Папки для поиска скриптов
    private List<string> scriptFolders = new List<string> { "Assets/Scripts", "Assets/Editor" };
    
    // Папки для сканирования ассетов (по умолчанию весь Assets)
    private string assetRootFolder = "Assets"; 
    
    private Vector2 scrollPos;

    [MenuItem("Tools/Project Context Exporter (AI Helper)")]
    public static void ShowWindow()
    {
        GetWindow<ProjectContextExporter>("AI Context Exporter");
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        GUILayout.Label("Настройки Экспорта", EditorStyles.boldLabel);

        // 1. Определение текущего контекста
        string contextMode = "All Open Scenes";
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) contextMode = "Open Prefab";
        else if (Selection.activeGameObject != null) contextMode = "Selected Object Only";
        
        EditorGUILayout.HelpBox($"Режим: {contextMode}\n(Откройте несколько сцен 'Additive', чтобы экспортировать их вместе)", MessageType.Info);

        // 2. Опции
        includeTOC = EditorGUILayout.Toggle("1. Добавить Оглавление", includeTOC);
        includeHierarchy = EditorGUILayout.Toggle("2. Иерархия объектов (Scene)", includeHierarchy);
        if (includeHierarchy)
        {
            EditorGUI.indentLevel++;
            expandPrefabsInHierarchy = EditorGUILayout.Toggle("Раскрывать Префабы", expandPrefabsInHierarchy);
            EditorGUI.indentLevel--;
        }
        
        includeProjectMap = EditorGUILayout.Toggle("3. Карта Связей (Refs)", includeProjectMap);
        includeScriptContents = EditorGUILayout.Toggle("4. Содержимое Скриптов", includeScriptContents);
        
        // --- НОВЫЙ TOGGLE ---
        includeProjectAssets = EditorGUILayout.Toggle("5. Структура Ассетов (Project)", includeProjectAssets);

        // 3. Выбор папок скриптов
        if (includeScriptContents)
        {
            GUILayout.Space(10);
            GUILayout.Label("Папки для чтения кода:", EditorStyles.boldLabel);
            for (int i = 0; i < scriptFolders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                scriptFolders[i] = EditorGUILayout.TextField(scriptFolders[i]);
                if (GUILayout.Button("X", GUILayout.Width(20))) scriptFolders.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Добавить папку кода")) scriptFolders.Add("Assets/");
        }

        GUILayout.Space(20);

        // 4. Кнопки действий
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("КОПИРОВАТЬ В БУФЕР", GUILayout.Height(40))) ExportToClipboard();
        GUI.backgroundColor = Color.white;
        
        if (GUILayout.Button("Сохранить в файл...", GUILayout.Height(20))) ExportToFile();

        EditorGUILayout.EndScrollView();
    }

    private void ExportToClipboard()
    {
        string result = GenerateFullReport();
        GUIUtility.systemCopyBuffer = result;
        Debug.Log($"<color=green>Context скопирован! ({result.Length} символов)</color>");
    }

    private void ExportToFile()
    {
        string result = GenerateFullReport();
        string path = EditorUtility.SaveFilePanel("Save Context", "", "project_context", "txt");
        if (!string.IsNullOrEmpty(path)) 
        {
            File.WriteAllText(path, result);
            Debug.Log($"<color=green>Файл сохранен: {path}</color>");
            EditorUtility.RevealInFinder(path);
        }
    }

    private string GenerateFullReport()
    {
        StringBuilder sb = new StringBuilder();

        // --- Поиск скриптов ---
        List<string> scriptFiles = new List<string>();
        if (includeScriptContents) scriptFiles = FindAllScriptFiles();

        // ==========================================
        // 1. ОГЛАВЛЕНИЕ (TOC)
        // ==========================================
        if (includeTOC)
        {
            sb.AppendLine("=== PROJECT STRUCTURE & TOC ===");
            if (includeHierarchy) sb.AppendLine("- HIERARCHY TREE (Scene View)");
            if (includeProjectMap) sb.AppendLine("- COMPONENT REFERENCE MAP");
            if (includeProjectAssets) sb.AppendLine("- PROJECT ASSETS STRUCTURE (Folders)"); // Добавлено в TOC
            if (includeScriptContents)
            {
                sb.AppendLine("- CODEBASE FILES:");
                foreach (var file in scriptFiles) sb.AppendLine($"  -- {file}");
            }
            sb.AppendLine("===============================\n");
        }

        // Подготовка корней для обхода
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        GameObject selectedObj = Selection.activeGameObject;

        // ==========================================
        // 2. ИЕРАРХИЯ (HIERARCHY)
        // ==========================================
        if (includeHierarchy)
        {
            sb.AppendLine("=== HIERARCHY VIEW ===");
            sb.AppendLine("Legend: (INACTIVE) = Disabled in Inspector | (HIDDEN) = Hidden in Scene View");

            if (prefabStage != null)
            {
                sb.AppendLine($"--- [PREFAB MODE: {prefabStage.prefabContentsRoot.name}] ---");
                TraverseHierarchy(prefabStage.prefabContentsRoot.transform, sb, "", true);
            }
            else if (selectedObj != null)
            {
                sb.AppendLine($"--- [SELECTED OBJECT: {selectedObj.name}] ---");
                TraverseHierarchy(selectedObj.transform, sb, "", expandPrefabsInHierarchy);
            }
            else
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded)
                    {
                        sb.AppendLine($"\n--- [SCENE: {scene.name}] ---");
                        var roots = scene.GetRootGameObjects();
                        foreach (var root in roots)
                        {
                            TraverseHierarchy(root.transform, sb, "", expandPrefabsInHierarchy);
                        }
                    }
                }
            }
            sb.AppendLine("======================\n");
        }

        // ==========================================
        // 3. КАРТА СВЯЗЕЙ (PROJECT MAP)
        // ==========================================
        if (includeProjectMap)
        {
            sb.AppendLine("=== COMPONENT CONNECTIONS MAP ===");
            List<Transform> mapRoots = new List<Transform>();

            if (prefabStage != null) mapRoots.Add(prefabStage.prefabContentsRoot.transform);
            else if (selectedObj != null) mapRoots.Add(selectedObj.transform);
            else
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded)
                    {
                        foreach (var root in scene.GetRootGameObjects()) mapRoots.Add(root.transform);
                    }
                }
            }

            foreach (var root in mapRoots) BuildReferenceMap(root, sb);
            sb.AppendLine("=================================\n");
        }

        // ==========================================
        // 5. СТРУКТУРА АССЕТОВ (PROJECT VIEW) - НОВЫЙ БЛОК
        // ==========================================
        if (includeProjectAssets)
        {
            sb.AppendLine("=== PROJECT ASSETS STRUCTURE ===");
            sb.AppendLine("Legend: Lists files in Assets folder (excluding .meta)");
            TraverseProjectFolders(assetRootFolder, sb, "");
            sb.AppendLine("================================\n");
        }

        // ==========================================
        // 4. ИСХОДНЫЙ КОД (SCRIPTS)
        // ==========================================
        if (includeScriptContents)
        {
            sb.AppendLine("=== SOURCE CODE FILES ===");
            foreach (var filePath in scriptFiles)
            {
                sb.AppendLine($"---");
                sb.AppendLine(filePath);
                sb.AppendLine("---");
                try
                {
                    string content = File.ReadAllText(filePath);
                    sb.AppendLine(content);
                }
                catch (System.Exception e) { sb.AppendLine($"// Error reading file: {e.Message}"); }
                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    // --- ЛОГИКА ОБХОДА ИЕРАРХИИ ---
    private void TraverseHierarchy(Transform obj, StringBuilder sb, string indent, bool expandPrefabs)
    {
        string prefabInfo = "";
        if (PrefabUtility.IsAnyPrefabInstanceRoot(obj.gameObject))
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(obj.gameObject);
            prefabInfo = $"[PREFAB: {(source != null ? source.name : "Missing")}] ";
        }

        string activeStatus = obj.gameObject.activeSelf ? "" : "(INACTIVE) ";
        bool isHiddenInScene = SceneVisibilityManager.instance.IsHidden(obj.gameObject);
        string visibilityStatus = isHiddenInScene ? "(HIDDEN) " : "";
        string fullStatus = $"{activeStatus}{visibilityStatus}";
        string components = GetComponentsString(obj.gameObject);
        
        sb.AppendLine($"{indent}{fullStatus}{obj.name} {prefabInfo}{components}");

        bool isPrefabRoot = PrefabUtility.IsAnyPrefabInstanceRoot(obj.gameObject);
        if (isPrefabRoot && !expandPrefabs && indent.Length > 0)
        {
            sb.AppendLine($"{indent}  [...Prefab Hierarchy Hidden...]");
            return;
        }

        foreach (Transform child in obj)
        {
            TraverseHierarchy(child, sb, indent + "  ", expandPrefabs);
        }
    }

    private string GetComponentsString(GameObject go)
    {
        var comps = go.GetComponents<Component>()
            .Where(c => c != null && !(c is Transform))
            .Select(c => c.GetType().Name)
            .ToArray();
        
        if (comps.Length == 0) return "";
        return $" ({string.Join(", ", comps)})";
    }

    // --- ЛОГИКА КАРТЫ СВЯЗЕЙ ---
    private void BuildReferenceMap(Transform obj, StringBuilder sb)
    {
        var components = obj.GetComponents<MonoBehaviour>();
        foreach (var mb in components)
        {
            if (mb == null) continue;

            SerializedObject so = new SerializedObject(mb);
            SerializedProperty prop = so.GetIterator();
            List<string> refs = new List<string>();

            bool enter = true;
            while (prop.NextVisible(enter))
            {
                enter = false;
                if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue != null)
                {
                    if (prop.name == "m_Script") continue;
                    refs.Add($"{prop.name} -> {prop.objectReferenceValue.name} ({prop.objectReferenceValue.GetType().Name})");
                }
            }

            if (refs.Count > 0)
            {
                string activeStatus = obj.gameObject.activeSelf ? "" : "(INACTIVE) ";
                sb.AppendLine($"obj: {activeStatus}{obj.name} [{mb.GetType().Name}]");
                foreach (var r in refs)
                {
                    sb.AppendLine($"    -> {r}");
                }
            }
        }

        foreach (Transform child in obj)
        {
            BuildReferenceMap(child, sb);
        }
    }

    // --- ЛОГИКА СТРУКТУРЫ АССЕТОВ (НОВАЯ) ---
    private void TraverseProjectFolders(string path, StringBuilder sb, string indent)
    {
        if (!Directory.Exists(path)) return;

        // Получаем информацию о директории
        DirectoryInfo dirInfo = new DirectoryInfo(path);
        
        // Получаем подпапки
        var dirs = dirInfo.GetDirectories();
        foreach (var dir in dirs)
        {
            // Игнорируем скрытые папки или ненужные (можно расширить фильтр)
            if (dir.Name.StartsWith(".")) continue;

            sb.AppendLine($"{indent}[Folder] {dir.Name}/");
            TraverseProjectFolders(dir.FullName, sb, indent + "  ");
        }

        // Получаем файлы
        var files = dirInfo.GetFiles();
        foreach (var file in files)
        {
            if (file.Extension == ".meta") continue; // Игнорируем .meta
            if (file.Extension == ".DS_Store") continue;

            // Выводим имя файла с расширением
            sb.AppendLine($"{indent}- {file.Name}");
        }
    }

    // --- ПОИСК СКРИПТОВ ---
    private List<string> FindAllScriptFiles()
    {
        List<string> files = new List<string>();
        foreach (var folder in scriptFolders)
        {
            if (Directory.Exists(folder))
            {
                var found = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);
                files.AddRange(found);
            }
        }
        return files.Select(f => f.Replace("\\", "/")).Distinct().ToList();
    }
}