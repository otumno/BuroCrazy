// Assets/Editor/DialogueJSONImporter.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using DialogueSystem.Data;

public class DialogueJSONImporter : EditorWindow
{
    string jsonInput = "";
    string targetFolder = "Assets/Scripts/DialogueSystem/DialoguesBase/Generated"; // Путь по умолчанию обновлен
    Vector2 scrollPos;

    [MenuItem("Tools/AI Toolset/Dialogue Importer (Compact)")]
    public static void ShowWindow() => GetWindow<DialogueJSONImporter>("AI Dialogue");

    void OnGUI()
    {
        GUILayout.Label("AI Dialogue Importer v2.1 (FIXED)", EditorStyles.boldLabel);
        GUILayout.Label("Формат: Compact (i, t, m, s, x...)", EditorStyles.miniLabel);
        
        targetFolder = EditorGUILayout.TextField("Target Folder:", targetFolder);
        
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        jsonInput = EditorGUILayout.TextArea(jsonInput, GUILayout.Height(300));
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("GENERATE GRAPH", GUILayout.Height(40)))
        {
            GenerateGraph();
        }
    }

    void GenerateGraph()
    {
        if (string.IsNullOrEmpty(jsonInput)) return;

        GraphData data = null;
        try { data = JsonUtility.FromJson<GraphData>(jsonInput); }
        catch { Debug.LogError("JSON Error"); return; }

        if (data == null || string.IsNullOrEmpty(data.name)) { Debug.LogError("Bad JSON Data"); return; }

        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
        
        string assetPath = $"{targetFolder}/{data.name}.asset";
        
        // 1. Создаем Граф
        DialogueGraph graph = ScriptableObject.CreateInstance<DialogueGraph>();
        AssetDatabase.CreateAsset(graph, assetPath);

        var idMap = new Dictionary<string, DialogueNode>();
        var links = new List<LinkReq>();

        // 2. Создаем ноды
        Vector2 pos = Vector2.zero;
        foreach (var n in data.n)
        {
            DialogueNode node = null;
            pos += new Vector2(300, 0); 
            if (pos.x > 1500) { pos.x = 0; pos.y += 250; } // Сетка

            switch (n.t)
            {
                case "start": // START node (обрабатываем start явно)
                    var s = graph.CreateNode<StartNode>();
                    if (!string.IsNullOrEmpty(n.dt) && System.Enum.TryParse(n.dt, out DialogueSystem.Data.DialogueType parsedDt))
                        s.dialogueType = parsedDt;
                    links.Add(new LinkReq { src = s, nextId = n.x });
                    node = s;
                    break;

                case "p": // Phrase
                    var p = graph.CreateNode<PhraseNode>();
                    p.text = n.m;
                    p.speakerID = n.s;
                    if (n.vt != null && n.vt.Length > 0)
                        p.variantTexts = new System.Collections.Generic.List<string>(n.vt);
                    links.Add(new LinkReq { src = p, nextId = n.x });
                    node = p;
                    break;
                
                case "c": // Choice
                    var c = graph.CreateNode<ChoiceNode>();
                    c.queryText = n.m;
                    if(n.o != null) foreach(var opt in n.o) {
                        var o = new ChoiceNode.ChoiceOption { text = opt.m, conditionKey = opt.key, operation = opt.op, conditionValue = opt.v };
                        c.options.Add(o);
                        links.Add(new LinkReq { src = c, nextId = opt.x, optIndex = c.options.Count-1 });
                    }
                    node = c; 
                    break;

                case "e": // Event
                    var e = graph.CreateNode<EventNode>();
                    if (System.Enum.TryParse(n.et, out EventNode.EventType et)) e.eventType = et;
                    e.flagKey = n.key; 
                    e.intValue = n.v; 
                    e.notificationText = n.m;
                    links.Add(new LinkReq { src = e, nextId = n.x });
                    node = e; 
                    break;

                case "if": // Condition
                    var cond = graph.CreateNode<ConditionNode>();
                    cond.conditionKey = n.key; cond.operation = n.op; cond.conditionValue = n.v;
                    links.Add(new LinkReq { src = cond, nextId = n.trueX, isTrueBranch = true });
                    links.Add(new LinkReq { src = cond, nextId = n.falseX, isTrueBranch = false });
                    node = cond; 
                    break;
                
                case "r": // Random
                    var rnd = graph.CreateNode<RandomNode>();
                    if(n.o != null) foreach(var opt in n.o) {
                        var ro = new RandomNode.RandomOutcome { chance = (float)opt.v / 100f }; // v=50 -> 0.5
                        rnd.outcomes.Add(ro);
                        links.Add(new LinkReq { src = rnd, nextId = opt.x, optIndex = rnd.outcomes.Count-1 });
                    }
                    node = rnd; 
                    break;

                case "end": 
                    node = graph.CreateNode<EndNode>(); 
                    break;
            }

            if (node != null) {
                node.name = $"{n.t}_{n.i}"; // Даем ноде имя для удобства в инспекторе
                node.graphPosition = new Rect(pos, Vector2.zero);
                idMap[n.i] = node;
                
                // !!! ВАЖНОЕ ИСПРАВЛЕНИЕ: Помечаем ноду как измененную !!!
                EditorUtility.SetDirty(node); 
            }
        }

        // 3. Установка стартовой ноды
        if (idMap.ContainsKey(data.root))
        {
            if (idMap[data.root] is StartNode sn) graph.startNode = sn; // Если root это start node
            else 
            {
                // Если root указывает сразу на фразу (старый формат), создаем старт автоматически
                var autoStart = graph.CreateNode<StartNode>();
                autoStart.graphPosition = new Rect(-300, 0, 0, 0);
                autoStart.nextNode = idMap[data.root];
                graph.startNode = autoStart;
                EditorUtility.SetDirty(autoStart);
            }
        }

        // 4. Линковка
        foreach (var l in links)
        {
            if (string.IsNullOrEmpty(l.nextId) || !idMap.ContainsKey(l.nextId)) continue;
            var target = idMap[l.nextId];

            if (l.src is StartNode sn) sn.nextNode = target;
            else if (l.src is PhraseNode pn) pn.nextNode = target;
            else if (l.src is EventNode en) en.nextNode = target;
            else if (l.src is ChoiceNode cn) cn.options[l.optIndex].nextNode = target;
            else if (l.src is RandomNode rn) rn.outcomes[l.optIndex].nextNode = target;
            else if (l.src is ConditionNode cond) {
                if (l.isTrueBranch) cond.trueNode = target; else cond.falseNode = target;
            }
            
            // После линковки снова помечаем, что нода изменилась
            EditorUtility.SetDirty(l.src);
        }

        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=green>Граф '{data.name}' создан успешно в {assetPath}!</color>");
        
        // Пингуем файл, чтобы показать где он
        var createdObj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
        EditorGUIUtility.PingObject(createdObj);
    }

    // JSON DTO Classes
    [System.Serializable] class GraphData { public string name; public string root; public List<N> n; }
    [System.Serializable] class N {
        public string i; // id
        public string t; // type
        public string m; // message
        public string s; // speaker
        public string x; // next
        public string et; // eventType
        public string key; // flagKey
        public string op; // operation
        public int v; // value
        public string trueX; // true next
        public string falseX; // false next
        public List<O> o; // options
        public string dt; // dialogueType: "World" или "Phone"
        public string[] vt; // variantTexts для PhraseNode
    }
    [System.Serializable] class O { public string m; public string x; public string key; public string op; public int v; }
    class LinkReq { public DialogueNode src; public string nextId; public int optIndex; public bool isTrueBranch; }
}