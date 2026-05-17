// === FILE: Assets/Scripts/Cinematic/Editor/CinematicNodeView.cs ===
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Reflection;

namespace CinematicSystem.Editor
{
    /// <summary>
    /// Визуальное представление узла в редакторе Cinematic Graph.
    /// Переработанный UI с цветами, иконками и foldout для каждого типа узла.
    /// </summary>
    public class CinematicNodeView : Node
    {
        // === Константы цветов для типов узлов ===
        private static readonly Color StartNodeColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        private static readonly Color EndNodeColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        private static readonly Color MoveToNodeColor = new Color(0.15f, 0.35f, 0.85f, 0.9f);
        private static readonly Color TeleportNodeColor = new Color(0.4f, 0.15f, 0.6f, 0.9f);
        private static readonly Color SayBubbleNodeColor = new Color(0.1f, 0.6f, 0.7f, 0.9f);
        private static readonly Color SayDialogNodeColor = new Color(0.85f, 0.4f, 0.1f, 0.9f);
        private static readonly Color CallDialogueNodeColor = new Color(0.85f, 0.65f, 0.1f, 0.9f);
        private static readonly Color CameraNodeColor = new Color(0.7f, 0.1f, 0.7f, 0.9f);
        private static readonly Color WaitNodeColor = new Color(0.7f, 0.7f, 0.1f, 0.9f);
        private static readonly Color WaitClickNodeColor = new Color(0.2f, 0.7f, 0.3f, 0.9f);
        private static readonly Color ConditionNodeColor = new Color(0.55f, 0.15f, 0.65f, 0.9f);
        private static readonly Color EventNodeColor = new Color(0.75f, 0.15f, 0.15f, 0.9f);
        private static readonly Color SpawnNodeColor = new Color(0.1f, 0.55f, 0.25f, 0.9f);
        private static readonly Color CommentNodeColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        /// <summary>Текущий узел</summary>
        public CinematicNode Node { get; private set; }
        
        /// <summary>GraphView</summary>
        private CinematicGraphView graphView;
        
        /// <summary>Порт входа</summary>
        private Port inputPort;
        
        /// <summary>Порт выхода</summary>
        private Port outputPort;
        
        /// <summary>Выпадающая панель (foldout)</summary>
        private VisualElement foldoutPanel;
        
        /// <summary>Сохраненное состояние foldout</summary>
        private bool isExpanded = true;

        /// <summary>Публичный доступ к OutputPort</summary>
        public Port OutputPort => outputPort;

        /// <summary>
        /// Конструктор визуального представления узла.
        /// </summary>
        public CinematicNodeView(CinematicNode node, CinematicGraphView view)
        {
            Node = node;
            graphView = view;
            
            // Заголовок с типом и подписью
            string shortDesc = GetShortDescription(node);
            title = $"{GetNodeTypeIcon(node.GetNodeType())} {GetNodeTypeName(node.GetNodeType())}";
            if (!string.IsNullOrEmpty(shortDesc))
            {
                title += $"  ▸ {shortDesc}";
            }
            
            // Цвет фона по типу узла
            Color nodeColor = GetNodeColor(node.GetNodeType());
            
            // Применяем цвет к заголовку
            titleContainer.style.backgroundColor = nodeColor;
            
            // Стиль самого узла
            style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            style.borderTopLeftRadius = 6;
            style.borderTopRightRadius = 6;
            style.borderBottomLeftRadius = 6;
            style.borderBottomRightRadius = 6;
            
            // Создаём порты
            CreatePorts();
            
            // Создаём основную панель с foldout
            CreateMainContent();
            
            // Устанавливаем viewDataKey для сохранения состояния
            viewDataKey = node.id;
        }

        /// <summary>
        /// Получить иконку для типа узла (юникодные символы).
        /// </summary>
        private string GetNodeTypeIcon(string nodeType)
        {
            switch (nodeType)
            {
                case "start": return "▶";
                case "end": return "■";
                case "move": return "→";
                case "teleport": return "∅";
                case "say_bubble": return "💬";
                case "say_dialog": return "🗨";
                case "call_dialogue": return "📞";
                case "call_cinematic": return "🔗";
                case "camera": return "🎥";
                case "wait": return "⏱";
                case "wait_click": return "👆";
                case "wait_despawn": return "💀";
                case "condition": return "⚖";
                case "event": return "⚡";
                case "spawn": return "👤";
                case "random": return "🎲";
                case "comment": return "📝";
                default: return "●";
            }
        }

        /// <summary>
        /// Получить название типа узла для отображения.
        /// </summary>
        private string GetNodeTypeName(string nodeType)
        {
            switch (nodeType)
            {
                case "start": return "START";
                case "end": return "END";
                case "move": return "Move To";
                case "teleport": return "Teleport";
                case "say_bubble": return "Say Bubble";
                case "say_dialog": return "Say Dialog";
                case "call_dialogue": return "Call Dialogue";
                case "call_cinematic": return "Call Graph";
                case "camera": return "Camera";
                case "wait": return "Wait";
                case "wait_click": return "Wait Click";
                case "wait_despawn": return "Wait Despawn";
                case "condition": return "Condition";
                case "event": return "Event";
                case "spawn": return "Spawn";
                case "random": return "Random";
                case "comment": return "Comment";
                default: return nodeType.ToUpper();
            }
        }

        /// <summary>
        /// Получить краткое описание узла (для заголовка).
        /// </summary>
        private string GetShortDescription(CinematicNode node)
        {
            if (node is Nodes.MoveToNode move)
                return move.targetKey ?? "";
            if (node is Nodes.SayBubbleNode bubble)
                return bubble.text?.Length > 20 ? bubble.text.Substring(0, 20) + "..." : bubble.text ?? "";
            if (node is Nodes.SayDialogNode dialog)
                return dialog.speakerName ?? "";
            if (node is Nodes.ConditionNode cond)
                return $"{cond.conditionKey} {cond.operation} {cond.value}";
            if (node is Nodes.EventNode evt)
                return evt.eventType.ToString();
            if (node is Nodes.StartNode start)
                return start.characterID ?? "";
            if (node is Nodes.CommentNode comment)
                return comment.comment?.Length > 15 ? comment.comment.Substring(0, 15) + "..." : "";
            if (node is Nodes.RandomNode rand)
                return rand.outcomes != null ? $"{rand.outcomes.Count} исходов" : "";
            if (node is Nodes.CallCinematicGraphNode call)
                return call.targetGraph?.name ?? "";
            if (node is Nodes.WaitForCharacterDespawnNode despawn)
                return despawn.characterKey ?? "";
            return "";
        }

        /// <summary>
        /// Получить цвет для типа узла.
        /// </summary>
        private Color GetNodeColor(string nodeType)
        {
            switch (nodeType)
            {
                case "start": return StartNodeColor;
                case "end": return EndNodeColor;
                case "move": return MoveToNodeColor;
                case "teleport": return TeleportNodeColor;
                case "say_bubble": return SayBubbleNodeColor;
                case "say_dialog": return SayDialogNodeColor;
                case "call_dialogue": return CallDialogueNodeColor;
                case "call_cinematic": return new Color(0.4f, 0.6f, 0.9f, 0.9f);
                case "camera": return CameraNodeColor;
                case "wait": return WaitNodeColor;
                case "wait_click": return WaitClickNodeColor;
                case "wait_despawn": return new Color(0.5f, 0.2f, 0.5f, 0.9f);
                case "condition": return ConditionNodeColor;
                case "event": return EventNodeColor;
                case "spawn": return SpawnNodeColor;
                case "random": return new Color(0.9f, 0.7f, 0.1f, 0.9f);
                case "comment": return CommentNodeColor;
                default: return new Color(0.3f, 0.3f, 0.3f);
            }
        }

        /// <summary>
        /// Создать порты ввода/вывода.
        /// </summary>
        private void CreatePorts()
        {
            // Входной порт (для всех узлов кроме Start)
            if (Node.GetNodeType() != "start")
            {
                inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                inputPort.portName = "In";
                inputContainer.Add(inputPort);
            }
            
            // Выходной порт (для всех узлов кроме End)
            if (Node.GetNodeType() != "end")
            {
                // Для ConditionNode создаём два выходных порта
                if (Node is Nodes.ConditionNode)
                {
                    var truePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                    truePort.portName = "T";
                    truePort.portColor = new Color(0.4f, 1f, 0.4f);
                    outputContainer.Add(truePort);
                    
                    var falsePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                    falsePort.portName = "F";
                    falsePort.portColor = new Color(1f, 0.4f, 0.4f);
                    outputContainer.Add(falsePort);
                }
                // Для RandomNode создаём множественные выходные порты
                else if (Node is Nodes.RandomNode randNode)
                {
                    if (randNode.outcomes != null && randNode.outcomes.Count > 0)
                    {
                        for (int i = 0; i < randNode.outcomes.Count; i++)
                        {
                            var outPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                            outPort.portName = i.ToString();
                            outPort.portColor = new Color(0.9f, 0.7f, 0.1f);
                            outputContainer.Add(outPort);
                        }
                    }
                    else
                    {
                        // Если исходов нет, создаём один порт по умолчанию
                        outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                        outputPort.portName = "Out";
                        outputContainer.Add(outputPort);
                    }
                }
                else
                {
                    outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                    outputPort.portName = "Out";
                    outputContainer.Add(outputPort);
                }
            }
            
            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary>
        /// Создать основное содержимое узла с IMGUIContainer и foldout.
        /// </summary>
        private void CreateMainContent()
        {
            // Создаём IMGUIContainer для каждого типа узла
            var imguiContainer = new IMGUIContainer(() => DrawNodeIMGUI());
            imguiContainer.style.marginTop = 5;
            imguiContainer.style.marginBottom = 5;
            imguiContainer.style.paddingLeft = 5;
            imguiContainer.style.paddingRight = 5;
            
            extensionContainer.Add(imguiContainer);
            RefreshExpandedState();
        }

        /// <summary>
        /// Отрисовка узла через IMGUI (вызывается каждый кадр в редакторе).
        /// </summary>
        private void DrawNodeIMGUI()
        {
            // Проверяем что узел не удалён
            if (Node == null) return;
            
            EditorGUILayout.BeginVertical();
            
            // Рисуем поля в зависимости от типа узла
            DrawNodeFields();
            
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Нарисовать поля узла в зависимости от его типа.
        /// </summary>
        private void DrawNodeFields()
        {
            // Для StartNode
            if (Node is Nodes.StartNode startNode)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Character ID", EditorStyles.boldLabel);
                startNode.characterID = EditorGUILayout.TextField(startNode.characterID);
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField($"→ {startNode.nextNode?.nodeName ?? "null"}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для EndNode
            if (Node is Nodes.EndNode endNode)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("End Node", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Завершает выполнение графа", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для MoveToNode
            if (Node is Nodes.MoveToNode moveTo)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
                moveTo.targetKey = EditorGUILayout.TextField("Waypoint Key", moveTo.targetKey);
                moveTo.characterID = EditorGUILayout.TextField("Character", moveTo.characterID);
                
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                moveTo.speed = EditorGUILayout.FloatField("Speed", moveTo.speed);
                if (moveTo.speed <= 0) EditorGUILayout.LabelField("(Auto)", GUILayout.Width(50));
                EditorGUILayout.EndHorizontal();
                
                moveTo.waitForCompletion = EditorGUILayout.Toggle("Wait", moveTo.waitForCompletion);
                moveTo.usePathfinding = EditorGUILayout.Toggle("Pathfinding", moveTo.usePathfinding);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для TeleportNode
            if (Node is Nodes.TeleportNode tp)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Teleport", EditorStyles.boldLabel);
                tp.targetKey = EditorGUILayout.TextField("Target Key", tp.targetKey);
                tp.characterID = EditorGUILayout.TextField("Character", tp.characterID);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для SayBubbleNode
            if (Node is Nodes.SayBubbleNode bubble)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Say Bubble", EditorStyles.boldLabel);
                bubble.speakerID = EditorGUILayout.TextField("Speaker", bubble.speakerID);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Text", GUILayout.Width(50));
                bubble.text = EditorGUILayout.TextArea(bubble.text, GUILayout.Height(40));
                EditorGUILayout.EndHorizontal();
                
                bubble.duration = EditorGUILayout.FloatField("Duration", bubble.duration);
                if (bubble.duration <= 0)
                    EditorGUILayout.LabelField("(Click to dismiss)", EditorStyles.miniLabel);
                
                // Новое поле для voice clip
                EditorGUILayout.Space(2);
                bubble.voiceClip = (AudioClip)EditorGUILayout.ObjectField("Voice Clip", bubble.voiceClip, typeof(AudioClip), false);
                
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для SayDialogNode
            if (Node is Nodes.SayDialogNode dialog)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Say Dialog", EditorStyles.boldLabel);
                dialog.speakerName = EditorGUILayout.TextField("Speaker Name", dialog.speakerName);
                dialog.portrait = (Sprite)EditorGUILayout.ObjectField("Portrait", dialog.portrait, typeof(Sprite), false);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Text", GUILayout.Width(50));
                dialog.text = EditorGUILayout.TextArea(dialog.text, GUILayout.Height(40));
                EditorGUILayout.EndHorizontal();
                
                dialog.duration = EditorGUILayout.FloatField("Duration", dialog.duration);
                dialog.voiceClip = (AudioClip)EditorGUILayout.ObjectField("Voice", dialog.voiceClip, typeof(AudioClip), false);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для CallDialogueNode
            if (Node is Nodes.CallDialogueNode call)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Call Dialogue", EditorStyles.boldLabel);
                call.dialogueGraph = (DialogueSystem.Data.DialogueGraph)EditorGUILayout.ObjectField("Dialogue Graph", call.dialogueGraph, typeof(DialogueSystem.Data.DialogueGraph), false);
                call.targetClient = (ClientPathfinding)EditorGUILayout.ObjectField("Target Client", call.targetClient, typeof(ClientPathfinding), true);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для CameraNode
            if (Node is Nodes.CameraNode cam)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
                cam.targetKey = EditorGUILayout.TextField("Target Key", cam.targetKey);
                cam.orthographicSize = EditorGUILayout.FloatField("Size", cam.orthographicSize);
                cam.duration = EditorGUILayout.FloatField("Duration", cam.duration);
                cam.useDirectorCamera = EditorGUILayout.Toggle("Director Cam", cam.useDirectorCamera);
                cam.waitForCompletion = EditorGUILayout.Toggle("Wait", cam.waitForCompletion);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для WaitForSecondsNode
            if (Node is Nodes.WaitForSecondsNode wait)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Wait", EditorStyles.boldLabel);
                wait.seconds = EditorGUILayout.FloatField("Seconds", wait.seconds);
                wait.useRealtime = EditorGUILayout.Toggle("Realtime", wait.useRealtime);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для WaitForUIClickNode
            if (Node is Nodes.WaitForUIClickNode click)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Wait UI Click", EditorStyles.boldLabel);
                click.uiElementKey = EditorGUILayout.TextField("UI Element", click.uiElementKey);
                click.timeout = EditorGUILayout.FloatField("Timeout", click.timeout);
                if (click.timeout <= 0)
                    EditorGUILayout.LabelField("(No timeout)", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для ConditionNode
            if (Node is Nodes.ConditionNode cond)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Condition", EditorStyles.boldLabel);
                cond.conditionKey = EditorGUILayout.TextField("Key", cond.conditionKey);
                cond.operation = EditorGUILayout.TextField("Op", cond.operation);
                cond.value = EditorGUILayout.IntField("Value", cond.value);
                
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("True:", GUILayout.Width(40));
                EditorGUILayout.LabelField(cond.trueNode?.nodeName ?? "null", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("False:", GUILayout.Width(40));
                EditorGUILayout.LabelField(cond.falseNode?.nodeName ?? "null", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для EventNode
            if (Node is Nodes.EventNode evt)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Event", EditorStyles.boldLabel);
                
                // Выпадающий список для типа события
                evt.eventType = (Nodes.EventType)EditorGUILayout.EnumPopup("Type", evt.eventType);
                
                EditorGUILayout.Space(2);
                
                // Динамические поля в зависимости от типа
                switch (evt.eventType)
                {
                    case Nodes.EventType.AddMoney:
                    case Nodes.EventType.AddInfluence:
                        evt.intValue = EditorGUILayout.IntField("Value", evt.intValue);
                        break;
                        
                    case Nodes.EventType.SetFlag:
                        evt.stringValue = EditorGUILayout.TextField("Flag Name", evt.stringValue);
                        evt.intValue = EditorGUILayout.IntField("Value", evt.intValue);
                        break;
                        
                    case Nodes.EventType.PlaySound:
                        // Новое поле soundClip
                        evt.soundClip = (AudioClip)EditorGUILayout.ObjectField("Sound Clip", evt.soundClip, typeof(AudioClip), false);
                        if (evt.soundClip == null)
                            evt.intValue = EditorGUILayout.IntField("Sound ID", evt.intValue);
                        break;
                        
                    case Nodes.EventType.PlayMusic:
                        // Новое поле musicClip
                        evt.musicClip = (AudioClip)EditorGUILayout.ObjectField("Music Clip", evt.musicClip, typeof(AudioClip), false);
                        evt.restorePreviousMusic = EditorGUILayout.Toggle("Restore After", evt.restorePreviousMusic);
                        break;
                        
                    case Nodes.EventType.RestorePreviousMusic:
                        EditorGUILayout.LabelField("Restores previous music track", EditorStyles.miniLabel);
                        break;
                        
                    case Nodes.EventType.ActivateObject:
                    case Nodes.EventType.DeactivateObject:
                        evt.targetObjectKey = EditorGUILayout.TextField("Object Key", evt.targetObjectKey);
                        break;
                        
                    case Nodes.EventType.ApplyTrait:
                    case Nodes.EventType.RemoveTrait:
                        evt.characterID = EditorGUILayout.TextField("Character", evt.characterID);
                        evt.stringValue = EditorGUILayout.TextField("Trait", evt.stringValue);
                        break;
                        
                    case Nodes.EventType.SetSpeedMultiplier:
                        evt.characterID = EditorGUILayout.TextField("Character", evt.characterID);
                        evt.intValue = EditorGUILayout.IntField("Multiplier", evt.intValue);
                        break;
                        
                    case Nodes.EventType.LockControl:
                    case Nodes.EventType.UnlockControl:
                        evt.boolValue = EditorGUILayout.Toggle("Value", evt.boolValue);
                        break;
                        
                    case Nodes.EventType.SetCursor:
                        evt.boolValue = EditorGUILayout.Toggle("Show Cursor", evt.boolValue);
                        break;
                        
                    case Nodes.EventType.ShowNotification:
                        evt.stringValue = EditorGUILayout.TextField("Message", evt.stringValue);
                        break;
                        
                    case Nodes.EventType.OpenDirectorDesk:
                    case Nodes.EventType.CloseDirectorDesk:
                    case Nodes.EventType.StopMusic:
                    case Nodes.EventType.AddStrike:
                        // Нет дополнительных полей
                        break;
                        
                    default:
                        evt.intValue = EditorGUILayout.IntField("Int", evt.intValue);
                        evt.stringValue = EditorGUILayout.TextField("String", evt.stringValue);
                        evt.boolValue = EditorGUILayout.Toggle("Bool", evt.boolValue);
                        break;
                }
                
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для SpawnCharacterNode
            if (Node is Nodes.SpawnCharacterNode spawn)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Spawn Character", EditorStyles.boldLabel);
                spawn.archetypeID = EditorGUILayout.TextField("Archetype", spawn.archetypeID);
                spawn.spawnPointKey = EditorGUILayout.TextField("Spawn Point", spawn.spawnPointKey);
                spawn.targetKeyForReference = EditorGUILayout.TextField("Reference Key", spawn.targetKeyForReference);
                
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Forced Goal", EditorStyles.miniLabel);
                spawn.forcedGoal = (ClientGoal)EditorGUILayout.EnumPopup("", spawn.forcedGoal);
                
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для RandomNode
            if (Node is Nodes.RandomNode rand)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Random", EditorStyles.boldLabel);
                
                if (rand.outcomes == null)
                    rand.outcomes = new System.Collections.Generic.List<Nodes.RandomOutcome>();
                
                // Кнопка добавить исход
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+", GUILayout.Width(25), GUILayout.Height(20)))
                {
                    rand.outcomes.Add(new Nodes.RandomOutcome { weight = 1f });
                    EditorUtility.SetDirty(rand);
                    
                    // Обновляем порты визуально
                    RefreshPorts();
                    RefreshExpandedState();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(2);
                
                // Отображаем все исходы
                for (int i = 0; i < rand.outcomes.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal("box");
                    
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField($"Исход {i}", EditorStyles.miniLabel);
                    
                    // Вес в формате 0-1
                    rand.outcomes[i].weight = EditorGUILayout.Slider(
                        rand.outcomes[i].weight, 0f, 1f);
                    
                    // Ссылка на следующий узел
                    rand.outcomes[i].nextNode = (CinematicNode)EditorGUILayout.ObjectField(
                        "Next", rand.outcomes[i].nextNode, typeof(CinematicNode), false);
                    
                    EditorGUILayout.EndVertical();
                    
                    // Кнопка удалить исход
                    if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(35)))
                    {
                        rand.outcomes.RemoveAt(i);
                        EditorUtility.SetDirty(rand);
                        
                        // Обновляем порты визуально
                        RefreshPorts();
                        RefreshExpandedState();
                    }
                    
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(2);
                }
                
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для CommentNode
            if (Node is Nodes.CommentNode comment)
            {
                EditorGUILayout.BeginVertical("box");
                comment.comment = EditorGUILayout.TextArea(comment.comment, GUILayout.Height(60));
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Для неизвестных типов - базовые поля
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Node ID: " + Node.id, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Type: " + Node.GetNodeType(), EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Получить входной порт по имени.
        /// </summary>
        public Port GetInputPort(string name = "")
        {
            return inputPort;
        }
    }
}