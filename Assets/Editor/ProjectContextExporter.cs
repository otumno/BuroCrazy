using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

public class ProjectContextExporter : EditorWindow
{
    private int currentTab = 4; // Открываем сразу новую вкладку с логами
    private string[] tabNames = { "1. Классика", "2. Карта", "3. Скрипты", "4. Файлы", "5. Парсер Лога" };

    // --- Настройки Классической Вкладки (V1) ---
    private bool includeTOC = true;
    private bool includeHierarchy = true;
    private bool expandPrefabsInHierarchy = true;
    private bool includeProjectMap = true;
    private bool includeScriptContents = true;
    private bool includeProjectAssets = true; 

    private List<string> scriptFolders = new List<string> { "Assets/Scripts", "Assets/Editor" };
    private string assetRootFolder = "Assets"; 
    
    // --- Настройки Вкладки Точечных Скриптов ---
    private string selectiveScriptsInput = "AgentMover\nStaffController\nClientStateMachine";

    // --- Настройки Вкладки Парсера Логов ---
    private string rawLogInput = "";

    private Vector2 scrollPos;

    [MenuItem("Tools/Project Context Exporter (AI Helper V8)")]
    public static void ShowWindow()
    {
        GetWindow<ProjectContextExporter>("AI Exporter V8");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        currentTab = GUILayout.Toolbar(currentTab, tabNames, GUILayout.Height(30));
        GUILayout.Space(10);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (currentTab != 4)
        {
            GUILayout.Label("Базовая информация (включается в дамп)", EditorStyles.boldLabel);
            includeHierarchy = EditorGUILayout.Toggle("Иерархия Сцены", includeHierarchy);
            includeProjectMap = EditorGUILayout.Toggle("Дерево Файлов", includeProjectMap);
            EditorGUILayout.Space();
        }

        if (currentTab == 0)
        {
            EditorGUILayout.HelpBox("ВНИМАНИЕ: Будет собран КОД ВООБЩЕ ВСЕХ СКРИПТОВ в проекте.", MessageType.Warning);
            includeTOC = EditorGUILayout.Toggle("Добавить Оглавление", includeTOC);
            includeProjectMap = EditorGUILayout.Toggle("Карта Связей (Refs)", includeProjectMap);
            includeScriptContents = EditorGUILayout.Toggle("Содержимое Скриптов", includeScriptContents);
            includeProjectAssets = EditorGUILayout.Toggle("Структура Ассетов", includeProjectAssets);
        }
        else if (currentTab == 1)
        {
            EditorGUILayout.HelpBox("Соберет ТОЛЬКО структуру проекта (сцена и список файлов).", MessageType.Info);
        }
        else if (currentTab == 2)
        {
            EditorGUILayout.HelpBox("Введи названия нужных скриптов (каждое с новой строки). Расширение .cs писать не обязательно.", MessageType.Info);
            selectiveScriptsInput = EditorGUILayout.TextArea(selectiveScriptsInput, GUILayout.Height(150));
        }
        else if (currentTab == 3)
        {
            EditorGUILayout.HelpBox("Выдаст плоский текстовый список всех скриптов и файлов проекта.", MessageType.Info);
        }
        else if (currentTab == 4)
        {
            // === НОВАЯ ФИЧА: СЖАТИЕ ЛОГОВ ===
            EditorGUILayout.HelpBox("Скопируй весь текст из окна Console в Unity (Ctrl+A -> Ctrl+C) и вставь сюда.\nПри экспорте утилита вырежет длинные стэктрейсы и оставит только суть, экономя токены AI.", MessageType.Info);
            rawLogInput = EditorGUILayout.TextArea(rawLogInput, GUILayout.Height(300));
        }

        EditorGUILayout.EndScrollView();
        GUILayout.Space(10);

        // === КНОПКИ ===
        GUILayout.BeginHorizontal();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button(currentTab == 4 ? "СЖАТЬ И СКОПИРОВАТЬ" : "КОПИРОВАТЬ В БУФЕР", GUILayout.Height(40))) 
        {
            string result = GenerateContent();
            GUIUtility.systemCopyBuffer = result;
            Debug.Log($"<color=green>[AI Exporter]</color> Данные скопированы! ({result.Length} символов)");
        }
        GUI.backgroundColor = Color.white;
        
        if (GUILayout.Button(currentTab == 4 ? "СЖАТЬ В ФАЙЛ..." : "СОХРАНИТЬ В ФАЙЛ...", GUILayout.Height(40))) 
        {
            string result = GenerateContent();
            string defaultName = currentTab == 4 ? "compressed_log" : "project_context";
            string path = EditorUtility.SaveFilePanel("Save Context", "", defaultName, "txt");
            if (!string.IsNullOrEmpty(path)) 
            {
                File.WriteAllText(path, result);
                Debug.Log($"<color=green>[AI Exporter]</color> Файл сохранен: {path}");
                EditorUtility.RevealInFinder(path);
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
    }

    private string GenerateContent()
    {
        if (currentTab == 3) return GenerateNamesListOnly();
        if (currentTab == 4) return CompressLog(rawLogInput);

        bool runTOC = includeTOC;
        bool runHier = includeHierarchy;
        bool runMap = includeProjectMap;
        bool runAssets = includeProjectAssets;
        bool runScripts = includeScriptContents;
        List<string> scriptsToRead = new List<string>();

        if (currentTab == 0)
        {
            if (runScripts) scriptsToRead = FindAllScriptFiles();
        }
        else if (currentTab == 1) 
        {
            runTOC = true; runHier = true; runMap = true; runAssets = true; runScripts = false;
        }
        else if (currentTab == 2) 
        {
            runTOC = false; runHier = false; runMap = false; runAssets = false; runScripts = true;
            scriptsToRead = GetSelectiveScripts();
        }

        StringBuilder sb = new StringBuilder();

        if (runTOC)
        {
            sb.AppendLine("=== AI CONTEXT EXPORT ===");
            sb.AppendLine($"Date: {DateTime.Now}");
            sb.AppendLine("=========================\n");
        }

        if (runHier)
        {
            sb.AppendLine("--- SCENE HIERARCHY ---");
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (GameObject root in activeScene.GetRootGameObjects())
            {
                TraverseHierarchy(root.transform, sb, "", expandPrefabsInHierarchy);
            }
            sb.AppendLine("======================\n");
        }

        if (runMap)
        {
            sb.AppendLine("--- COMPONENT CONNECTIONS MAP ---");
            Scene activeScene = SceneManager.GetActiveScene();
            foreach (GameObject root in activeScene.GetRootGameObjects())
            {
                BuildReferenceMap(root.transform, sb);
            }
            sb.AppendLine("=================================\n");
        }

        if (runAssets)
        {
            sb.AppendLine("--- PROJECT ASSETS STRUCTURE ---");
            TraverseProjectFolders(assetRootFolder, sb, "");
            sb.AppendLine("================================\n");
        }

        if (runScripts)
        {
            sb.AppendLine("=== SOURCE CODE FILES ===");
            if (scriptsToRead.Count == 0 && currentTab == 2)
            {
                sb.AppendLine("[ОШИБКА] Указанные скрипты не найдены!");
            }
            
            foreach (var filePath in scriptsToRead)
            {
                sb.AppendLine($"\n// === FILE: {filePath.Replace("\\", "/")} ===");
                try { sb.AppendLine(File.ReadAllText(filePath)); }
                catch (Exception e) { sb.AppendLine($"// Error: {e.Message}"); }
            }
        }

        return sb.ToString();
    }

    // === ПАРСЕР ЛОГОВ (НОВЫЙ АЛГОРИТМ СЖАТИЯ) ===
    private string CompressLog(string rawLog)
    {
        if (string.IsNullOrWhiteSpace(rawLog)) return "[ОШИБКА] Лог пуст. Вставьте текст из консоли.";

        string[] lines = rawLog.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder sb = new StringBuilder();
        
        sb.AppendLine("=== COMPRESSED UNITY LOG ===");
        sb.AppendLine($"Generated: {DateTime.Now}\n");

        int skippedLines = 0;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            
            // Фильтруем технический мусор Unity
            if (trimmed.StartsWith("UnityEngine.") || 
                trimmed.StartsWith("UnityEditor.") || 
                trimmed.StartsWith("System.") || 
                trimmed.StartsWith("DG.Tweening.") ||
                trimmed.Contains("MoveNext () (at Assets/") ||
                trimmed.Contains("InvokeMoveNext (System.Collections.") ||
                trimmed.StartsWith("(wrapper delegate-invoke)") ||
                (trimmed.Contains(".cs:") && trimmed.Contains("(at ")))
            {
                skippedLines++;
                continue;
            }

            sb.AppendLine(trimmed);
        }

        sb.AppendLine($"\n[Log Compressor: Вырезано {skippedLines} строк мусорного стэктрейса]");
        return sb.ToString();
    }

    // === ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ===
    private void TraverseHierarchy(Transform obj, StringBuilder sb, string indent, bool expandPrefabs)
    {
        string activeStatus = obj.gameObject.activeSelf ? "" : "(INACTIVE) ";
        sb.AppendLine($"{indent}{activeStatus}{obj.name}");
        foreach (Transform child in obj) TraverseHierarchy(child, sb, indent + "  ", expandPrefabs);
    }

    private void BuildReferenceMap(Transform obj, StringBuilder sb)
    {
        var components = obj.GetComponents<MonoBehaviour>();
        foreach (var mb in components)
        {
            if (mb == null) continue;
            SerializedObject so = new SerializedObject(mb);
            SerializedProperty prop = so.GetIterator();
            bool hasRefs = false;
            
            string componentLog = $"obj: {obj.name} [{mb.GetType().Name}]";
            StringBuilder refsLog = new StringBuilder();

            bool enter = true;
            while (prop.NextVisible(enter))
            {
                enter = false;
                if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue != null && prop.name != "m_Script")
                {
                    refsLog.AppendLine($"    -> {prop.name}: {prop.objectReferenceValue.name} ({prop.objectReferenceValue.GetType().Name})");
                    hasRefs = true;
                }
            }
            if (hasRefs) { sb.AppendLine(componentLog); sb.Append(refsLog.ToString()); }
        }
        foreach (Transform child in obj) BuildReferenceMap(child, sb);
    }

    private void TraverseProjectFolders(string path, StringBuilder sb, string indent)
    {
        if (!Directory.Exists(path)) return;
        DirectoryInfo dirInfo = new DirectoryInfo(path);
        
        foreach (var dir in dirInfo.GetDirectories())
        {
            if (dir.Name.StartsWith(".")) continue;
            sb.AppendLine($"{indent}[Folder] {dir.Name}/");
            TraverseProjectFolders(dir.FullName, sb, indent + "  ");
        }
        foreach (var file in dirInfo.GetFiles())
        {
            if (file.Extension == ".meta" || file.Extension == ".DS_Store") continue;
            sb.AppendLine($"{indent}- {file.Name}");
        }
    }

    private List<string> FindAllScriptFiles()
    {
        List<string> files = new List<string>();
        foreach (var folder in scriptFolders)
        {
            if (Directory.Exists(folder)) files.AddRange(Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories));
        }
        return files.Select(f => f.Replace("\\", "/")).Distinct().ToList();
    }

    private List<string> GetSelectiveScripts()
    {
        string[] targetNames = selectiveScriptsInput.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim().ToLower()).ToArray();

        string[] allScripts = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
        return allScripts.Where(s => targetNames.Contains(Path.GetFileNameWithoutExtension(s).ToLower()))
                         .Select(s => s.Replace("\\", "/")).ToList();
    }

    private string GenerateNamesListOnly()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== ALL PROJECT FILES ===");
        if (Directory.Exists("Assets"))
        {
            foreach (var f in Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")))
                sb.AppendLine(f.Replace("\\", "/"));
        }
        return sb.ToString();
    }
}
