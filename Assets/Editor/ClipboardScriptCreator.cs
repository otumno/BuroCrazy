using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

public class ClipboardScriptCreator
{
    // Горячая клавиша: Ctrl + Alt + S
    [MenuItem("Assets/Create/Script from Clipboard %&s")]
    public static void CreateScriptFromClipboard()
    {
        string clipboard = GUIUtility.systemCopyBuffer;

        if (string.IsNullOrWhiteSpace(clipboard))
        {
            Debug.LogWarning("Буфер обмена пуст!");
            return;
        }

        string[] lines = clipboard.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
        string firstLine = lines[0].Trim();
        
        string className = "";
        string fileContent = "";
        string finalPath = ""; 

        string cleanLine = firstLine.Replace("//", "").Trim();
        bool isHeaderNaming = firstLine.StartsWith("//") && firstLine.EndsWith(".cs");

        // --- 1. ПАРСИНГ ---
        if (lines.Length == 1 && !firstLine.StartsWith("//"))
        {
            // Случай: Просто имя
            className = Regex.Replace(firstLine, @"[^a-zA-Z0-9_]", "");
            finalPath = Path.Combine(GetCurrentAssetDirectory(), className + ".cs");
            
            // Первую строку НЕ берем в файл
            fileContent = 
$@"using UnityEngine;

public class {className} : MonoBehaviour
{{
    void Start() {{ }}
    void Update() {{ }}
}}";
        }
        else if (isHeaderNaming)
        {
            // Случай: Заголовок с путем/именем
            className = Path.GetFileNameWithoutExtension(cleanLine);

            if (cleanLine.Contains("/") || cleanLine.Contains("\\"))
                finalPath = cleanLine.Replace("\\", "/");
            else
                finalPath = Path.Combine(GetCurrentAssetDirectory(), cleanLine);

            // Берем ВСЕ строки (включая заголовок)
            fileContent = string.Join("\n", lines);
        }
        else
        {
            Debug.LogWarning("Формат не распознан. Ожидается '// Name.cs' или просто ИмяКласса.");
            return;
        }

        // Нормализуем путь для корректного сравнения (Unity использует forward slashes)
        finalPath = finalPath.Replace("\\", "/");


        // --- 2. ПРОВЕРКА НА ДУРАКА (Глобальный поиск дубликатов) ---
        // Ищем все скрипты с таким именем в проекте
        string[] foundGuids = AssetDatabase.FindAssets($"t:MonoScript {className}");
        
        foreach (string guid in foundGuids)
        {
            string existingPath = AssetDatabase.GUIDToAssetPath(guid);
            
            // Проверяем, что имя файла реально совпадает (FindAssets ищет вхождения, может найти 'Player' в 'PlayerController')
            if (Path.GetFileNameWithoutExtension(existingPath) == className)
            {
                // Если файл найден, и это НЕ тот файл, который мы собираемся писать/перезаписывать
                if (existingPath != finalPath)
                {
                    bool proceed = EditorUtility.DisplayDialog(
                        "КОНФЛИКТ ИМЕН!",
                        $"Внимание! Скрипт с классом '{className}' УЖЕ СУЩЕСТВУЕТ в другой папке:\n\n" +
                        $"{existingPath}\n\n" +
                        "Unity запрещает два класса с одинаковым именем.\n" +
                        "Создание этого файла приведет к ошибке компиляции.",
                        "Все равно создать (Риск)", // OK
                        "Отмена"                   // Cancel
                    );

                    if (!proceed) return; // Выход, если нажали Отмена
                }
            }
        }


        // --- 3. СОЗДАНИЕ ПАПОК ---
        string directoryPath = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            AssetDatabase.Refresh();
        }


        // --- 4. ПРОВЕРКА ЛОКАЛЬНОГО ФАЙЛА (Перезапись) ---
        if (File.Exists(finalPath))
        {
            int option = EditorUtility.DisplayDialogComplex(
                "Файл уже существует",
                $"Файл '{className}.cs' уже есть по этому пути. Перезаписать?",
                "Перезаписать!",      // 0
                "Отмена",             // 1
                "Открыть в редакторе" // 2
            );

            switch (option)
            {
                case 0: break; // Продолжаем выполнение (перезапись)
                case 1: return; 
                case 2: 
                    Object scriptAsset = AssetDatabase.LoadAssetAtPath<Object>(finalPath);
                    if (scriptAsset != null) AssetDatabase.OpenAsset(scriptAsset);
                    return; 
            }
        }

        // --- 5. ЗАПИСЬ ---
        File.WriteAllText(finalPath, fileContent);
        AssetDatabase.Refresh();
        
        Object createdAsset = AssetDatabase.LoadAssetAtPath<Object>(finalPath);
        if (createdAsset != null)
        {
            EditorGUIUtility.PingObject(createdAsset);
            Selection.activeObject = createdAsset;
        }
        
        Debug.Log($"Скрипт <b>{className}</b> успешно создан по пути: {finalPath}");
    }

    private static string GetCurrentAssetDirectory()
    {
        foreach (var obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;

            if (Directory.Exists(path))
                return path;
            else if (File.Exists(path))
                return Path.GetDirectoryName(path);
        }
        return "Assets";
    }
}