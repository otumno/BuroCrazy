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

    [MenuItem("Tools/AI Toolset/Smart Scene Builder")]
    public static void ShowWindow() => GetWindow<SmartSceneBuilder>("Scene Builder");

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Space(10);
        GUILayout.Label("1. Настройки", EditorStyles.boldLabel);
        
        // Логика выбора Root
        if (contextRoot == null && Selection.activeTransform != null) 
            contextRoot = Selection.activeTransform;
            
        contextRoot = (Transform)EditorGUILayout.ObjectField("Root (Single):", contextRoot, typeof(Transform), true);
        
        // --- НОВОЕ: Отображение мульти-выбора ---
        if (Selection.gameObjects.Length > 1)
        {
            EditorGUILayout.HelpBox($"Выбрано объектов: {Selection.gameObjects.Length}. Доступен пакетный режим.", MessageType.Info);
        }
        // ----------------------------------------

        updateExistingObjects = EditorGUILayout.ToggleLeft("Обновлять существующие", updateExistingObjects);

        GUILayout.Space(10);
        GUILayout.Label("2. JSON Input", EditorStyles.boldLabel);
        GUIStyle areaStyle = new GUIStyle(EditorStyles.textArea); areaStyle.wordWrap = true;
        
        string newJson = EditorGUILayout.TextArea(jsonInput, areaStyle, GUILayout.Height(150));
        if (newJson != jsonInput) { jsonInput = newJson; needsAnalysis = true; }

        if (GUILayout.Button("🔍 Анализировать JSON", GUILayout.Height(30))) AnalyzeJSON();

        if (instructionData != null && !needsAnalysis)
        {
            GUILayout.Space(15);
            int totalOps = (instructionData.operations != null ? instructionData.operations.Count : 0);
            
            string summary = $"Ops: {totalOps} | Scene: {countCreate + countModify} | Assets: {countAsset}";
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
                GUILayout.Label($"Укажите ссылки вручную ({assetMap.Count}):", EditorStyles.boldLabel);
                List<string> keys = new List<string>(assetMap.Keys);
                foreach (var key in keys) assetMap[key] = EditorGUILayout.ObjectField(key, assetMap[key], typeof(UnityEngine.Object), false);
            }
            
            GUILayout.Space(15);

            // --- НОВАЯ ЛОГИКА КНОПОК ---
            if (Selection.gameObjects.Length > 1)
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
                if (GUILayout.Button($"🚀 ВЫПОЛНИТЬ ДЛЯ ВСЕХ ({Selection.gameObjects.Length})", GUILayout.Height(40))) 
                {
                    BatchExecute();
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button("🚀 ВЫПОЛНИТЬ (Single)", GUILayout.Height(40))) 
                {
                    ExecuteSafe();
                }
            }
            GUI.backgroundColor = Color.white;
            // ---------------------------
        }
        EditorGUILayout.EndScrollView();
    }

    // --- НОВЫЙ МЕТОД ДЛЯ МАССОВОГО ПРИМЕНЕНИЯ ---
    void BatchExecute()
    {
        GameObject[] targets = Selection.gameObjects;
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName($"Batch Builder ({targets.Length})");
        int undoGroup = Undo.GetCurrentGroup();

        int successCount = 0;
        try
        {
            foreach (var go in targets)
            {
                // Подменяем контекст на текущий объект из списка
                contextRoot = go.transform;
                ExecuteInstructions(false); // false = не сохранять ассеты каждый раз (медленно)
                successCount++;
            }
            // Сохраняем один раз в конце
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>Успешно обновлено объектов: {successCount}</color>");
        }
        catch (Exception e)
        {
            Debug.LogError($"Batch Error: {e.Message}");
        }
        Undo.CollapseUndoOperations(undoGroup);
        GUIUtility.ExitGUI();
    }

    void ExecuteSafe()
    {
        try 
        { 
            ExecuteInstructions(true); 
            GUIUtility.ExitGUI();
        } 
        catch (Exception e) { Debug.LogError($"CRITICAL ERROR: {e.Message}\n{e.StackTrace}"); }
    }

    // ... (AnalyzeJSON, RemoveComments, NormalizeData, CollectRefs, ApplyProperty, ParseValue, FindType - ОСТАЮТСЯ БЕЗ ИЗМЕНЕНИЙ) ...
    // Вставь сюда остальные методы из предыдущей версии скрипта, они не менялись.
    // Единственное изменение ниже - добавлен аргумент saveAssets в ExecuteInstructions

    void AnalyzeJSON()
    {
        // ... (Код AnalyzeJSON из прошлого ответа) ...
        // Копирую сокращенно, чтобы не забивать ответ, логика та же
        try {
            string cleanJson = RemoveComments(jsonInput);
            instructionData = JsonUtility.FromJson<InstructionData>(cleanJson);
            NormalizeData(); 
            assetMap.Clear(); previewLog.Clear(); countCreate = 0; countModify = 0; countAsset = 0;
            if (instructionData == null || instructionData.operations == null) return;
            foreach(var op in instructionData.operations) {
                if (op.mode == "create") countCreate++; else if (op.mode == "modify") countModify++; else if (op.mode == "create_asset") countAsset++;
                string tName = string.IsNullOrEmpty(op.targetPath) ? (op.name ?? "ROOT") : op.targetPath;
                previewLog.Add($"[{op.mode.ToUpper()}] -> {tName}");
                if (op.components != null) foreach(var c in op.components) CollectRefs(c.properties);
                if (op.properties != null) CollectRefs(op.properties);
            }
            needsAnalysis = false;
        } catch (Exception e) { Debug.LogError($"JSON Error: {e.Message}"); }
    }

    // Добавил аргумент saveAssets
    void ExecuteInstructions(bool saveAssets = true)
    {
        foreach (var op in instructionData.operations) ProcessOperation(op);
        
        if (saveAssets)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Scene Builder: Завершено.");
        }
    }

    // Остальные методы (ProcessOperation, Handle..., Apply..., Parse..., ResolveTarget, FindType)
    // КОПИРУЮТСЯ ИЗ ПРЕДЫДУЩЕГО ОТВЕТА БЕЗ ИЗМЕНЕНИЙ.
    
    // --- ДЛЯ УДОБСТВА Я ВСТАВЛЮ ВАЖНЫЕ ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ НИЖЕ ---

    string RemoveComments(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;
        var lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var filteredLines = lines.Where(l => !l.TrimStart().StartsWith("//"));
        return string.Join("\n", filteredLines);
    }

    void NormalizeData()
    {
        if (instructionData == null) return;
        if (instructionData.operations == null) instructionData.operations = new List<OperationData>();
        if (instructionData.ops != null) instructionData.operations.AddRange(instructionData.ops);
        foreach (var op in instructionData.operations) {
            if (string.IsNullOrEmpty(op.mode)) op.mode = op.m;
            if (string.IsNullOrEmpty(op.targetPath)) op.targetPath = op.t;
            if (string.IsNullOrEmpty(op.name)) op.name = op.n;
            if (string.IsNullOrEmpty(op.type)) op.type = op.tp;
            if (op.components == null) op.components = new List<ComponentData>();
            if (op.c != null) op.components.AddRange(op.c);
            foreach (var comp in op.components) {
                if (string.IsNullOrEmpty(comp.type)) comp.type = comp.tp;
                if (comp.properties == null) comp.properties = new List<PropertyData>();
                if (comp.p != null) comp.properties.AddRange(comp.p);
                foreach (var prop in comp.properties) NormalizeProperty(prop);
            }
        }
    }
    void NormalizeProperty(PropertyData prop) { if (string.IsNullOrEmpty(prop.name)) prop.name = prop.nm; if (string.IsNullOrEmpty(prop.value)) prop.value = prop.v; }
    
    void CollectRefs(List<PropertyData> props) {
        if (props == null) return;
        foreach(var prop in props) {
            if(!string.IsNullOrEmpty(prop.value) && prop.value.StartsWith("$") && !prop.value.StartsWith("$Assets") && !assetMap.ContainsKey(prop.value))
                assetMap.Add(prop.value, null);
        }
    }

    void ProcessOperation(OperationData op)
    {
        if (op.mode == "create_asset") { HandleCreateAsset(op); return; }
        if ((op.mode == "modify" || op.mode == "create") && !string.IsNullOrEmpty(op.targetPath) && op.targetPath.StartsWith("Assets") && op.targetPath.EndsWith(".prefab")) { HandleModifyPrefab(op); return; }

        GameObject foundTarget = ResolveTarget(op.targetPath);
        if (op.mode == "create") HandleCreateSceneObject(op, foundTarget ? foundTarget.transform : null);
        else if (op.mode == "modify") {
            if (foundTarget == null) { Debug.LogError($"[MODIFY FAIL] Не найден объект: '{op.targetPath}'"); return; }
            HandleModifySceneObject(op, foundTarget);
        }
    }

    void HandleCreateAsset(OperationData op) { /* (Без изменений) */ }
    void HandleModifyPrefab(OperationData op) { /* (Без изменений) */ }

    void HandleCreateSceneObject(OperationData op, Transform parent)
    {
        GameObject go = null; bool reused = false;
        if (updateExistingObjects && !string.IsNullOrEmpty(op.name)) {
            Transform existing = parent ? parent.Find(op.name) : null;
            if (parent == null) { GameObject g = GameObject.Find(op.name); if (g && g.transform.parent == null) existing = g.transform; }
            if (existing) { go = existing.gameObject; reused = true; }
        }
        if (!reused) {
            go = new GameObject(string.IsNullOrEmpty(op.name) ? "NewObject" : op.name);
            Undo.RegisterCreatedObjectUndo(go, "Create");
            if (parent) GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
            if (parent && parent.GetComponent<RectTransform>() && !go.GetComponent<RectTransform>()) go.AddComponent<RectTransform>();
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

    void ApplyProperty(UnityEngine.Object obj, string propName, string val)
    {
        Type type = obj.GetType();
        FieldInfo field = type.GetField(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        PropertyInfo prop = type.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Type targetType = field != null ? field.FieldType : (prop != null ? prop.PropertyType : null);
        
        if (targetType == null) return; 

        bool isList = targetType.IsGenericType && typeof(IList).IsAssignableFrom(targetType);
        bool isArray = targetType.IsArray;

        if (isList || isArray) { /* (Логика массивов без изменений) */ }
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

    Type FindType(string name) 
    {
        if (string.IsNullOrEmpty(name)) return null;
        Type t = Type.GetType(name);
        if (t != null) return t;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var userAssembly = assemblies.FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        if (userAssembly != null) {
            var userType = userAssembly.GetTypes().FirstOrDefault(type => type.Name == name || type.FullName == name);
            if (userType != null) return userType;
        }
        foreach (var a in assemblies) {
            if (a.GetName().Name == "Assembly-CSharp") continue;
            foreach(var type in a.GetTypes()) {
                if(type.Name == name || type.FullName == name) return type;
            }
        }
        return null;
    }

    [Serializable] public class InstructionData { public List<OperationData> operations; public List<OperationData> ops; }
    [Serializable] public class OperationData { public string mode; public string m; public string targetPath; public string t; public string name; public string n; public string type; public string tp; public List<ComponentData> components; public List<ComponentData> c; public List<PropertyData> properties; public List<PropertyData> p; }
    [Serializable] public class ComponentData { public string type; public string tp; public List<PropertyData> properties; public List<PropertyData> p; }
    [Serializable] public class PropertyData { public string name; public string nm; public string value; public string v; }
}