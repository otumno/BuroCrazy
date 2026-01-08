// Assets/Editor/SmartSceneBuilder.cs
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using System.Linq;
using System.IO;

public class SmartSceneBuilder : EditorWindow
{
    // --- ИНТЕРФЕЙС ---
    string jsonInput = "";
    Vector2 scrollPos;
    bool showPromptSettings = false;
    bool showPreview = true;

    // --- ДАННЫЕ ---
    Dictionary<string, UnityEngine.Object> assetMap = new Dictionary<string, UnityEngine.Object>();
    Transform contextRoot; 
    bool updateExistingObjects = true;
    InstructionData instructionData;
    bool needsAnalysis = true;

    List<string> previewLog = new List<string>();
    int countCreate = 0;
    int countModify = 0;
    int countAsset = 0;

    private const string SYSTEM_PROMPT = @"Ты — Unity Scene Architect. Твоя задача — генерировать JSON для SmartSceneBuilder.
!!! ПРАВИЛА !!!
1. Unity JsonUtility НЕ понимает словари. Используй СПИСОК для свойств: ""properties"": [ { ""name"": ""x"", ""value"": ""y"" } ]
2. СПИСКИ (List/Array): Строка в скобках ""[Path1, Path2]""
3. ССЫЛКИ: 
   - Если указываешь полный путь к файлу (Assets/...), знак '$' НЕ нужен.
   - Используй '$Name' только если хочешь, чтобы я выбрал файл вручную.
4. РЕЖИМЫ: 'create', 'modify' (Сцена) и 'create_asset' (Файлы ScriptableObject).

ШАБЛОН:
{
  ""operations"": [
    {
      ""mode"": ""create_asset"",
      ""targetPath"": ""Assets/Data/Region_1.asset"", 
      ""type"": ""RegionData"",
      ""properties"": [ { ""name"": ""id"", ""value"": ""R1"" } ]
    },
    {
      ""mode"": ""modify"",
      ""targetPath"": ""Manager"",
      ""components"": [ { ""type"": ""GameManager"", ""properties"": [ { ""name"": ""regions"", ""value"": ""[Assets/Data/Region_1.asset]"" } ] } ]
    }
  ]
}";

    [MenuItem("Tools/Smart Scene Builder v6.2 (Arrays Fix)")]
    public static void ShowWindow() => GetWindow<SmartSceneBuilder>("Scene Builder");

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        showPromptSettings = EditorGUILayout.Foldout(showPromptSettings, "🤖 Промпт", true);
        if (showPromptSettings) {
            GUILayout.Label("Скопируйте в чат:", EditorStyles.miniLabel);
            if (GUILayout.Button("📋 Скопировать")) {
                GUIUtility.systemCopyBuffer = SYSTEM_PROMPT;
                ShowNotification(new GUIContent("Скопировано!"));
            }
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);
        GUILayout.Label("1. Настройки", EditorStyles.boldLabel);
        if (contextRoot == null && Selection.activeTransform != null) contextRoot = Selection.activeTransform;
        contextRoot = (Transform)EditorGUILayout.ObjectField("Root (Scene):", contextRoot, typeof(Transform), true);
        updateExistingObjects = EditorGUILayout.ToggleLeft("Обновлять существующие", updateExistingObjects);

        GUILayout.Space(10);
        GUILayout.Label("2. JSON", EditorStyles.boldLabel);
        GUIStyle areaStyle = new GUIStyle(EditorStyles.textArea); areaStyle.wordWrap = true;
        string newJson = EditorGUILayout.TextArea(jsonInput, areaStyle, GUILayout.Height(150));
        if (newJson != jsonInput) { jsonInput = newJson; needsAnalysis = true; }

        if (GUILayout.Button("🔍 Анализировать", GUILayout.Height(30))) AnalyzeJSON();

        if (instructionData != null && !needsAnalysis)
        {
            GUILayout.Space(15);
            string summary = $"Ops: {instructionData.operations.Count} | Scene: {countCreate + countModify} | Assets: {countAsset}";
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            showPreview = EditorGUILayout.Foldout(showPreview, summary, true);
            if (showPreview) {
                float logHeight = Mathf.Min(previewLog.Count * 20, 200);
                EditorGUILayout.BeginVertical(GUILayout.Height(logHeight)); 
                foreach (var log in previewLog) GUILayout.Label(log, EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();

            if (assetMap.Count > 0) {
                GUILayout.Space(10);
                GUILayout.Label($"Укажите вручную ({assetMap.Count}):", EditorStyles.boldLabel);
                List<string> keys = new List<string>(assetMap.Keys);
                foreach (var key in keys) assetMap[key] = EditorGUILayout.ObjectField(key, assetMap[key], typeof(UnityEngine.Object), false);
            }
            else {
                 GUILayout.Label("Ссылки автоматические или отсутствуют.", EditorStyles.miniLabel);
            }
            
            GUILayout.Space(15);
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("🚀 ВЫПОЛНИТЬ", GUILayout.Height(40))) 
            {
                try { ExecuteInstructions(); } 
                catch (Exception e) { Debug.LogError($"CRITICAL ERROR: {e.Message}\n{e.StackTrace}"); }
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndScrollView();
    }

    void AnalyzeJSON()
    {
        try {
            instructionData = JsonUtility.FromJson<InstructionData>(jsonInput);
            assetMap.Clear(); previewLog.Clear(); countCreate = 0; countModify = 0; countAsset = 0;

            if (instructionData?.operations == null) { Debug.LogError("Неверный JSON."); return; }

            foreach(var op in instructionData.operations) {
                if (op.mode == "create") countCreate++; 
                else if (op.mode == "modify") countModify++;
                else if (op.mode == "create_asset") countAsset++;
                
                string targetDisplayName = string.IsNullOrEmpty(op.targetPath) ? (op.name ?? "ROOT") : op.targetPath;
                previewLog.Add($"[{op.mode.ToUpper()}] -> {targetDisplayName}");

                if (op.components != null) foreach(var c in op.components) CollectRefs(c.properties);
                if (op.properties != null) CollectRefs(op.properties);
            }
            needsAnalysis = false;
        }
        catch (Exception e) { Debug.LogError($"JSON Error: {e.Message}"); }
    }
    
    void CollectRefs(List<PropertyData> props) {
        foreach(var prop in props) {
            if(!string.IsNullOrEmpty(prop.value)) {
                if (prop.value.StartsWith("$") && !prop.value.StartsWith("$Assets")) {
                    if (!assetMap.ContainsKey(prop.value)) assetMap.Add(prop.value, null);
                }
                if (prop.value.StartsWith("[") && prop.value.Contains("$")) {
                     string[] items = prop.value.Trim().Trim('[', ']').Split(',');
                     foreach(var item in items) {
                         string clean = item.Trim().Trim('"');
                         if (clean.StartsWith("$") && !clean.StartsWith("$Assets") && !assetMap.ContainsKey(clean))
                            assetMap.Add(clean, null);
                     }
                }
            }
        }
    }

    void ExecuteInstructions()
    {
        foreach (var op in instructionData.operations) ProcessOperation(op);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Scene Builder: Завершено.");
    }

    void ProcessOperation(OperationData op)
    {
        if (op.mode == "create_asset") {
            HandleCreateAsset(op);
            return;
        }

        GameObject foundTarget = ResolveTarget(op.targetPath);
        
        if (op.mode == "create") {
            HandleCreateSceneObject(op, foundTarget ? foundTarget.transform : null);
        }
        else if (op.mode == "modify") {
            if (foundTarget == null) {
                Debug.LogError($"[MODIFY FAIL] Не найден объект: '{op.targetPath}'");
                return;
            }
            HandleModifySceneObject(op, foundTarget);
        }
    }

    void HandleCreateAsset(OperationData op)
    {
        if (string.IsNullOrEmpty(op.targetPath) || !op.targetPath.StartsWith("Assets")) return;
        
        Type type = FindType(op.type);
        if (type == null) { Debug.LogError($"Class not found: {op.type}"); return; }

        string directory = Path.GetDirectoryName(op.targetPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(op.targetPath, type);
        bool isNew = false;
        
        if (asset == null) {
            asset = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, op.targetPath);
            isNew = true;
        }

        Undo.RecordObject(asset, "Update Asset");
        if (op.properties != null) foreach(var prop in op.properties) ApplyProperty(asset, prop.name, prop.value);
        
        if (isNew) {
            Debug.Log($"[ASSET CREATED] {op.targetPath}");
            // Важно: Импортируем сразу, чтобы другие операции могли его найти
            AssetDatabase.ImportAsset(op.targetPath);
        }
        EditorUtility.SetDirty(asset);
    }

    void HandleCreateSceneObject(OperationData op, Transform parent)
    {
        GameObject go = null;
        bool reused = false;

        if (updateExistingObjects && !string.IsNullOrEmpty(op.name)) {
            Transform existing = parent ? parent.Find(op.name) : null;
            if (parent == null) {
                GameObject g = GameObject.Find(op.name);
                if (g && g.transform.parent == null) existing = g.transform;
            }
            if (existing) { go = existing.gameObject; reused = true; }
        }

        if (!reused) {
            go = new GameObject(string.IsNullOrEmpty(op.name) ? "NewObject" : op.name);
            Undo.RegisterCreatedObjectUndo(go, "Create");
            if (parent) GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
            if (parent && parent.GetComponent<RectTransform>() && !go.GetComponent<RectTransform>())
                go.AddComponent<RectTransform>();
        } else Undo.RecordObject(go, "Update");

        ApplyComponents(go, op.components);
    }

    void HandleModifySceneObject(OperationData op, GameObject target)
    {
        Undo.RecordObject(target, "Modify");
        ApplyComponents(target, op.components);
    }

    void ApplyComponents(GameObject go, List<ComponentData> components)
    {
        if (components == null) return;
        foreach (var compData in components) {
            Type type = FindType(compData.type);
            if (type == null) { Debug.LogError($"Type not found: {compData.type}"); continue; }

            Component comp = go.GetComponent(type);
            if (!comp) comp = Undo.AddComponent(go, type);

            foreach (var prop in compData.properties) ApplyProperty(comp, prop.name, prop.value);
        }
    }

    // --- FIX: УЛУЧШЕННАЯ ОБРАБОТКА МАССИВОВ ---
    void ApplyProperty(UnityEngine.Object obj, string propName, string val)
    {
        Type type = obj.GetType();
        FieldInfo field = type.GetField(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        PropertyInfo prop = type.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Type targetType = field != null ? field.FieldType : (prop != null ? prop.PropertyType : null);
        
        if (targetType == null) return; 

        // Проверяем, является ли целевой тип Списком или Массивом
        bool isList = targetType.IsGenericType && typeof(IList).IsAssignableFrom(targetType);
        bool isArray = targetType.IsArray;

        if (isList || isArray) {
            // Определяем тип элемента
            Type itemType = isArray ? targetType.GetElementType() : targetType.GetGenericArguments()[0];
            
            // Создаем временный список для сбора данных
            var tempList = new List<object>();

            string cleanVal = val.Trim().Trim('[', ']');
            if (!string.IsNullOrEmpty(cleanVal)) {
                string[] items = cleanVal.Split(','); 
                foreach (string itemPath in items) {
                    object o = ParseValue(itemPath.Trim().Trim('"'), itemType);
                    if (o != null) tempList.Add(o);
                }
            }

            // ПРИСВАИВАНИЕ
            if (isArray) {
                // Если это массив -> создаем Array и копируем
                Array array = Array.CreateInstance(itemType, tempList.Count);
                for (int i = 0; i < tempList.Count; i++) array.SetValue(tempList[i], i);
                
                if (field != null) field.SetValue(obj, array); else prop.SetValue(obj, array);
            }
            else {
                // Если это List<T> -> создаем List и копируем
                IList listInstance = (IList)Activator.CreateInstance(targetType);
                foreach (var item in tempList) listInstance.Add(item);
                
                if (field != null) field.SetValue(obj, listInstance); else prop.SetValue(obj, listInstance);
            }
        }
        else {
            object finalVal = ParseValue(val, targetType);
            if (finalVal != null) {
                if (field != null) field.SetValue(obj, finalVal); else if (prop != null && prop.CanWrite) prop.SetValue(obj, finalVal);
            }
        }
    }

    object ParseValue(string val, Type targetType)
    {
        if (string.IsNullOrEmpty(val)) return null;
        
        // AUTO-LINK
        if (val.StartsWith("$Assets")) val = val.Substring(1);

        if (val.StartsWith("$")) return assetMap.ContainsKey(val) ? assetMap[val] : null;
        
        if (targetType == typeof(string)) return val;
        if (targetType == typeof(int)) return int.Parse(val);
        if (targetType == typeof(float)) return float.Parse(val, CultureInfo.InvariantCulture);
        if (targetType == typeof(bool)) return bool.Parse(val);
        if (targetType.IsEnum) return Enum.Parse(targetType, val);
        if (targetType == typeof(Vector3) || targetType == typeof(Vector2)) {
            string[] p = val.Trim('[', ']').Split(',');
            float x = float.Parse(p[0], CultureInfo.InvariantCulture);
            float y = p.Length > 1 ? float.Parse(p[1], CultureInfo.InvariantCulture) : 0;
            return targetType == typeof(Vector2) ? (object)new Vector2(x, y) : new Vector3(x, x, float.Parse(p.Length > 2 ? p[2] : "0", CultureInfo.InvariantCulture));
        }
        if (targetType == typeof(Color)) { Color c; ColorUtility.TryParseHtmlString(val, out c); return c; }

        if (typeof(UnityEngine.Object).IsAssignableFrom(targetType)) {
            if (val.StartsWith("Assets")) {
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(val, targetType);
                if (asset != null) return asset;
            }
            GameObject foundGO = ResolveTarget(val);
            if (foundGO == null) return null;
            if (targetType == typeof(GameObject)) return foundGO;
            if (typeof(Component).IsAssignableFrom(targetType)) return foundGO.GetComponent(targetType);
        }
        return null;
    }

    GameObject ResolveTarget(string path)
    {
        if (string.IsNullOrEmpty(path)) return contextRoot ? contextRoot.gameObject : null;
        if (contextRoot != null) {
            if (contextRoot.name.Equals(path, StringComparison.OrdinalIgnoreCase)) return contextRoot.gameObject;
            Transform child = contextRoot.Find(path);
            if (child != null) return child.gameObject;
        }
        return GameObject.Find(path);
    }

    Type FindType(string name) {
        if (string.IsNullOrEmpty(name)) return null;
        Type t = Type.GetType(name);
        if (t != null) return t;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) {
            foreach(var type in a.GetTypes()) if(type.Name == name || type.FullName == name) return type;
        }
        return null;
    }

    [Serializable] public class InstructionData { public List<OperationData> operations; }
    [Serializable] public class OperationData { 
        public string mode; public string targetPath; public string name; public string type; 
        public List<ComponentData> components; public List<PropertyData> properties; 
    }
    [Serializable] public class ComponentData { public string type; public List<PropertyData> properties; }
    [Serializable] public class PropertyData { public string name; public string value; }
}