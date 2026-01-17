// Assets/Editor/ClipboardScriptCreator.cs
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

public class ClipboardScriptCreator
{
    // Горячая клавиша: Ctrl + Alt + S
    [MenuItem("Assets/Create/File from Clipboard %&s")]
    public static void CreateFileFromClipboard()
    {
        string clipboard = GUIUtility.systemCopyBuffer;

        if (string.IsNullOrWhiteSpace(clipboard))
        {
            Debug.LogWarning("Буфер обмена пуст!");
            return;
        }

        string[] lines = clipboard.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
        string firstLine = lines[0].Trim();
        
        string fileName = "";
        string fileContent = "";
        string finalPath = ""; 

        string cleanLine = firstLine.Replace("//", "").Trim();
        
        // ПРОВЕРКА: Начинается с // и имеет расширение файла (например .cs, .asset, .json)
        bool isHeaderNaming = firstLine.StartsWith("//") && Path.HasExtension(cleanLine);

        // --- 1. ПАРСИНГ ---
        if (!isHeaderNaming)
        {
            // СТАРЫЙ РЕЖИМ: Если просто текст, создаем C# класс по умолчанию
            if (lines.Length == 1 && Regex.IsMatch(firstLine, @"^[a-zA-Z0-9_]+$"))
            {
                fileName = firstLine;
                finalPath = Path.Combine(GetCurrentAssetDirectory(), fileName + ".cs");
                
                fileContent = 
$@"using UnityEngine;

public class {fileName} : MonoBehaviour
{{
    void Start() {{ }}
    void Update() {{ }}
}}";
            }
            else
            {
                Debug.LogWarning("Формат не распознан. Первая строка должна быть '// Path/Name.ext' или просто ИмяКласса.");
                return;
            }
        }
        else
        {
            // НОВЫЙ РЕЖИМ: Берем путь и имя из первой строки
            fileName = Path.GetFileNameWithoutExtension(cleanLine);

            if (cleanLine.Contains("/") || cleanLine.Contains("\\"))
                finalPath = cleanLine.Replace("\\", "/");
            else
                finalPath = Path.Combine(GetCurrentAssetDirectory(), cleanLine);

            // Если это .asset или другой файл данных, нам НЕ нужна первая строка с комментарием в самом файле
            // Но для .cs она не мешает. 
            // Для чистоты YAML/JSON лучше пропустить первую строку, если это не C# скрипт.
            if (finalPath.EndsWith(".cs"))
            {
                 fileContent = string.Join("\n", lines);
            }
            else
            {
                // Для всех остальных файлов пропускаем строку с путем, чтобы не ломать формат (например, JSON не поддерживает //)
                // Но YAML поддерживает #, а у нас //. Лучше убрать.
                 fileContent = string.Join("\n", lines, 1, lines.Length - 1);
            }
        }

        // --- 2. СОЗДАНИЕ ПАПОК ---
        finalPath = finalPath.Replace("\\", "/");
        string directoryPath = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            AssetDatabase.Refresh();
        }

        if (File.Exists(finalPath))
        {
            int option = EditorUtility.DisplayDialogComplex("Файл существует", $"Перезаписать {Path.GetFileName(finalPath)}?", "Да", "Отмена", "Открыть");
            if (option == 1) return;
            if (option == 2) { 
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(finalPath); 
                if(asset) AssetDatabase.OpenAsset(asset); return; 
            }
        }

        // --- 3. ЗАПИСЬ ---
        File.WriteAllText(finalPath, fileContent);
        AssetDatabase.Refresh();
        
        Object createdAsset = AssetDatabase.LoadAssetAtPath<Object>(finalPath);
        if (createdAsset != null)
        {
            EditorGUIUtility.PingObject(createdAsset);
            Selection.activeObject = createdAsset;
        }
        Debug.Log($"Файл <b>{Path.GetFileName(finalPath)}</b> создан: {finalPath}");
    }

    private static string GetCurrentAssetDirectory()
    {
        foreach (var obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            if (Directory.Exists(path)) return path;
            else if (File.Exists(path)) return Path.GetDirectoryName(path);
        }
        return "Assets";
    }
}