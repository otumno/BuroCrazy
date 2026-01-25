using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using DialogueSystem.Data;

namespace DialogueSystem.Editor
{
    public class DialogueGraphView : GraphView
    {
        private DialogueEditorWindow _editorWindow;
        private DialogueGraph _graph;
        private VisualElement _helpBox;

        public DialogueGraphView(DialogueEditorWindow editorWindow)
        {
            _editorWindow = editorWindow;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            GenerateHelpBox();
        }

        // ---------------------------------------------------------
        // 1. СПРАВКА (HELP BOX)
        // ---------------------------------------------------------
        private void GenerateHelpBox()
        {
            _helpBox = new VisualElement();
            _helpBox.style.position = Position.Absolute;
            _helpBox.style.left = 15;
            _helpBox.style.top = 35;
            _helpBox.style.width = 400; // Чуть шире для текста
            _helpBox.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            
            Color borderCol = new Color(1, 1, 1, 0.2f);
            _helpBox.style.borderTopColor = borderCol; _helpBox.style.borderBottomColor = borderCol;
            _helpBox.style.borderLeftColor = borderCol; _helpBox.style.borderRightColor = borderCol;
            _helpBox.style.borderTopWidth = 1; _helpBox.style.borderBottomWidth = 1;
            _helpBox.style.borderLeftWidth = 1; _helpBox.style.borderRightWidth = 1;
            _helpBox.style.borderTopLeftRadius = 5; _helpBox.style.borderTopRightRadius = 5;
            _helpBox.style.borderBottomLeftRadius = 5; _helpBox.style.borderBottomRightRadius = 5;
            
            _helpBox.style.paddingTop = 10; _helpBox.style.paddingBottom = 10;
            _helpBox.style.paddingLeft = 10; _helpBox.style.paddingRight = 10;

            var title = new Label("СПРАВКА ПО НОДАМ") {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14, marginBottom = 5, color = new Color(0.4f, 1f, 0.4f) }
            };
            _helpBox.Add(title);

            // --- ОПИСАНИЕ ВОЗМОЖНОСТЕЙ ---
			
			AddHelpSection("0. СОЗДАНИЕ НОД", 
                "Основной функционал.\n" +
                "- Правой кнопкой мыши на любом месте в окне." +
				"- Начинаем нодой СТАРТ заканчиваем КОНЕЦ.\n" +
                "- В СТАРТе можно задать ФОН ПО УМОЛЧАНИЮ для всех нод.");
			
            AddHelpSection("1. ФРАЗА (PHRASE)", 
                "Базовая реплика.\n" +
                "- Speaker ID: 'Director', 'Client' или ID из базы NPC.\n" +
                "- Appear Sound: Звук 'вжик' при появлении.\n" +
                "- Voice/Portrait: Можно переопределить дефолтные.\n" +
                "- Node Image: Показать изображение в этой ноде.");

            AddHelpSection("2. ВЫБОР (CHOICE)", 
                "Ветвление диалога кнопками.\n" +
                "- Условия (Condition): Кнопка появится, ТОЛЬКО если условие верно.\n" +
                "  Пример Key: 'MONEY', Op: '>=', Val: 100.\n" +
                "  Пример Key: 'MET_INSPECTOR', Op: '==', Val: 1.\n" +
                "- Node Image: Показать изображение в этой ноде.");

            AddHelpSection("3. СОБЫТИЕ (EVENT)", 
                "Изменение состояния игры.\n" +
                "- SetFlag: Запомнить выбор. Key: 'HELPED_GRANNY', Val: 1.\n" +
                "  (Используется для спавна клиентов на след. день!)\n" +
                "- AddMoney: Дать/забрать деньги (100 или -50).\n" +
                "- AddStrike: Выдать страйк директору.\n" +
                "- Notification: Если заполнить текст, покажет окно 'РЕЗУЛЬТАТ'.\n" +
                "- Node Image: Показать изображение в этой ноде.");

            AddHelpSection("4. СЛУЧАЙНОСТЬ (RANDOM)", 
                "Автоматический выбор пути.\n" +
                "- Chance: Вес вероятности (чем больше, тем чаще).\n" +
                "  Используется для проверок удачи или вариативности.");

            AddHelpSection("КАК СДЕЛАТЬ КВЕСТ 'ПРИХОДИТЕ ЗАВТРА':", 
                "1. В диалоге сегодня: Нода EVENT -> SetFlag 'COME_BACK' = 1.\n" +
                "2. В базе SpecialVisitors (Resources):\n" +
                "   - Создать посетителя на День X+1.\n" +
                "   - Required Flag Key: 'COME_BACK'.\n" +
                "   - Required Flag Value: 1.\n" +
                "   - Forced Goal: DirectorApproval.");

            var closeBtn = new Button(() => ToggleHelp()) { text = "Закрыть справку" };
            closeBtn.style.marginTop = 10;
            closeBtn.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            _helpBox.Add(closeBtn);

            Add(_helpBox);
            _helpBox.visible = EditorPrefs.GetBool("Bureau_ShowHelp", true);
        }

        private void AddHelpSection(string header, string body)
        {
            var h = new Label(header) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8, color = new Color(0.8f, 0.8f, 0.8f) } };
            _helpBox.Add(h);
            var b = new Label(body) { style = { whiteSpace = WhiteSpace.Normal, fontSize = 11, color = new Color(0.7f, 0.7f, 0.7f) } };
            _helpBox.Add(b);
        }

        public void ToggleHelp()
        {
            if (_helpBox == null) return;
            _helpBox.visible = !_helpBox.visible;
            EditorPrefs.SetBool("Bureau_ShowHelp", _helpBox.visible);
        }

        // ---------------------------------------------------------
        // 2. ОСНОВНАЯ ЛОГИКА ГРАФА
        // ---------------------------------------------------------

        public void PopulateView(DialogueGraph graph)
        {
            _graph = graph;
            graphViewChanged -= OnGraphViewChanged;
            DeleteElements(graphElements);
            graphViewChanged += OnGraphViewChanged;

            if (!this.Contains(_helpBox)) Add(_helpBox);
            _helpBox.BringToFront();

            foreach (var node in _graph.allNodes) CreateNodeView(node);
            foreach (var node in _graph.allNodes) RestoreConnections(node);
        }

        private void CreateNodeView(DialogueNode nodeData)
        {
            var nodeView = new Node
            {
                title = nodeData.name,
                viewDataKey = nodeData.id,
                style = { left = nodeData.graphPosition.x, top = nodeData.graphPosition.y, minWidth = 200 }
            };
            nodeView.capabilities |= Capabilities.Resizable;

            // Входной порт для всех, кроме Старта
            if (!(nodeData is StartNode))
            {
                var input = GeneratePort(nodeView, Direction.Input, Port.Capacity.Multi);
                input.portName = "Вход";
                nodeView.inputContainer.Add(input);
            }

            // --- ОТРИСОВКА НОД ---

            if (nodeData is StartNode start)
            {
                nodeView.title = "СТАРТ";
                nodeView.mainContainer.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f, 0.8f);
                var outPort = GeneratePort(nodeView, Direction.Output, Port.Capacity.Single);
                outPort.portName = "Начало";
                nodeView.outputContainer.Add(outPort);
                
                var soundContainer = new IMGUIContainer(() => {
                    start.startSoundOverride = (AudioClip)EditorGUILayout.ObjectField("Звук старта:", start.startSoundOverride, typeof(AudioClip), false);
                });
                nodeView.extensionContainer.Add(soundContainer);

                var bgContainer = new IMGUIContainer(() => {
                    start.defaultBackground = (Sprite)EditorGUILayout.ObjectField("Фон по умолчанию:", start.defaultBackground, typeof(Sprite), false);
                });
                nodeView.extensionContainer.Add(bgContainer);
                
                nodeView.capabilities &= ~Capabilities.Deletable;
            }
            else if (nodeData is EndNode end)
            {
                nodeView.title = "КОНЕЦ";
                nodeView.mainContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
				var soundContainer = new IMGUIContainer(() => {
                    GUILayout.Space(5);
                    end.endSound = (AudioClip)EditorGUILayout.ObjectField("Звук конца:", end.endSound, typeof(AudioClip), false);
                });
                nodeView.extensionContainer.Add(soundContainer);
            }
            else if (nodeData is PhraseNode phrase)
            {
                nodeView.title = "ФРАЗА";
                nodeView.mainContainer.style.backgroundColor = new Color(0.2f, 0.35f, 0.5f, 0.8f);

                AddTextField(nodeView, "Кто говорит (ID):", phrase.speakerID, v => phrase.speakerID = v, phrase);
                AddTextField(nodeView, "Текст:", phrase.text, v => phrase.text = v, phrase, true);

                var outPort = GeneratePort(nodeView, Direction.Output, Port.Capacity.Single);
                outPort.portName = "Далее";
                nodeView.outputContainer.Add(outPort);

                var extras = new IMGUIContainer(() => {
                    GUILayout.Space(5);
                    GUILayout.Label("Дополнительно:", EditorStyles.boldLabel);
                    phrase.appearSound = (AudioClip)EditorGUILayout.ObjectField("Звук 'Вжик':", phrase.appearSound, typeof(AudioClip), false);
                    phrase.voiceClip = (AudioClip)EditorGUILayout.ObjectField("Спец. голос:", phrase.voiceClip, typeof(AudioClip), false);
                    phrase.speakerPortrait = (Sprite)EditorGUILayout.ObjectField("Спец. портрет:", phrase.speakerPortrait, typeof(Sprite), false);
                    phrase.nodeImage = (Sprite)EditorGUILayout.ObjectField("Изображение ноды:", phrase.nodeImage, typeof(Sprite), false);
                });
                nodeView.extensionContainer.Add(extras);
            }
            else if (nodeData is EventNode evt)
            {
                nodeView.title = "СОБЫТИЕ";
                nodeView.mainContainer.style.backgroundColor = new Color(0.5f, 0.2f, 0.2f, 0.8f);

                var typeField = new EnumField("Тип:", evt.eventType);
                typeField.RegisterValueChangedCallback(e => { evt.eventType = (EventNode.EventType)e.newValue; EditorUtility.SetDirty(evt); });
                nodeView.extensionContainer.Add(typeField);

                AddTextField(nodeView, "Имя Флага (Key):", evt.flagKey, v => evt.flagKey = v, evt);
                
                var valContainer = new VisualElement() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                valContainer.Add(new Label("Значение:") { style = { width = 70 } });
                var valField = new IntegerField() { value = evt.intValue, style = { flexGrow = 1 } };
                valField.RegisterValueChangedCallback(e => { evt.intValue = e.newValue; EditorUtility.SetDirty(evt); });
                valContainer.Add(valField);
                nodeView.extensionContainer.Add(valContainer);

                AddTextField(nodeView, "Текст игроку:", evt.notificationText, v => evt.notificationText = v, evt, true);

                var outPort = GeneratePort(nodeView, Direction.Output, Port.Capacity.Single);
                outPort.portName = "Далее";
                nodeView.outputContainer.Add(outPort);
                
                var soundContainer = new IMGUIContainer(() => {
                    evt.soundEffect = (AudioClip)EditorGUILayout.ObjectField("Звук эффекта:", evt.soundEffect, typeof(AudioClip), false);
                    evt.nodeImage = (Sprite)EditorGUILayout.ObjectField("Изображение ноды:", evt.nodeImage, typeof(Sprite), false);
                });
                nodeView.extensionContainer.Add(soundContainer);
            }
            else if (nodeData is ChoiceNode choice)
            {
                nodeView.title = "ВЫБОР";
                nodeView.mainContainer.style.backgroundColor = new Color(0.2f, 0.4f, 0.3f, 0.8f);

                AddTextField(nodeView, "Мысли ГГ:", choice.queryText, v => choice.queryText = v, choice, true);

                var addBtn = new Button(() => { 
                    choice.options.Add(new ChoiceNode.ChoiceOption { text = "Ответ..." });
                    EditorUtility.SetDirty(choice); PopulateView(_graph);
                }) { text = "+ Добавить вариант" };
                nodeView.extensionContainer.Add(addBtn);

                var imageContainer = new IMGUIContainer(() => {
                    choice.nodeImage = (Sprite)EditorGUILayout.ObjectField("Изображение ноды:", choice.nodeImage, typeof(Sprite), false);
                });
                nodeView.extensionContainer.Add(imageContainer);

                for (int i = 0; i < choice.options.Count; i++)
                {
                    int idx = i;
                    var box = CreateOptionBox();

                    var header = new VisualElement() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 5 } };
                    header.Add(new Label($"Вариант {i + 1}") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                    var del = new Button(() => { choice.options.RemoveAt(idx); EditorUtility.SetDirty(choice); PopulateView(_graph); }) { text = "X" };
                    del.style.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
                    header.Add(del);
                    box.Add(header);

                    var txt = new TextField() { value = choice.options[i].text, multiline = true };
                    txt.RegisterValueChangedCallback(e => { choice.options[idx].text = e.newValue; EditorUtility.SetDirty(choice); });
                    txt.style.whiteSpace = WhiteSpace.Normal;
                    box.Add(txt);

                    var condBox = new VisualElement() { style = { flexDirection = FlexDirection.Row, marginTop = 5, alignItems = Align.Center } };
                    condBox.Add(new Label("Если:") { style = { width = 35, fontSize = 10 } });
                    
                    var flagField = new TextField() { value = choice.options[i].conditionKey, tooltip = "Имя флага (напр. MONEY)" };
                    flagField.style.flexGrow = 1; 
                    flagField.style.minWidth = 50;
                    flagField.RegisterValueChangedCallback(e => { choice.options[idx].conditionKey = e.newValue; EditorUtility.SetDirty(choice); });
                    condBox.Add(flagField);

                    var opField = new TextField() { value = choice.options[i].operation, tooltip = "==, >, <, >=" };
                    opField.style.width = 30; opField.style.marginLeft = 2; opField.style.marginRight = 2;
                    if (string.IsNullOrEmpty(choice.options[i].operation)) choice.options[idx].operation = "==";
                    opField.RegisterValueChangedCallback(e => { choice.options[idx].operation = e.newValue; EditorUtility.SetDirty(choice); });
                    condBox.Add(opField);

                    var valFieldCondition = new IntegerField() { value = choice.options[i].conditionValue };
                    valFieldCondition.style.width = 40;
                    valFieldCondition.RegisterValueChangedCallback(e => { choice.options[idx].conditionValue = e.newValue; EditorUtility.SetDirty(choice); });
                    condBox.Add(valFieldCondition);

                    box.Add(condBox);

                    var port = GeneratePort(nodeView, Direction.Output, Port.Capacity.Single);
                    port.userData = idx;
                    port.portName = ""; 
                    port.style.alignSelf = Align.FlexEnd;
                    port.style.marginTop = 5;
                    box.Add(port);

                    nodeView.extensionContainer.Add(box);
                }
            }
            // --- ДОБАВЛЕНО: RANDOM NODE ---
            else if (nodeData is RandomNode rnd)
            {
                nodeView.title = "СЛУЧАЙНОСТЬ";
                nodeView.mainContainer.style.backgroundColor = new Color(0.4f, 0.2f, 0.5f, 0.8f);

                AddTextField(nodeView, "Комментарий:", rnd.developerComment, v => rnd.developerComment = v, rnd, true);

                var addBtn = new Button(() => { 
                    rnd.outcomes.Add(new RandomNode.RandomOutcome { chance = 1f });
                    EditorUtility.SetDirty(rnd); 
                    PopulateView(_graph);
                }) { text = "+ Добавить исход" };
                nodeView.extensionContainer.Add(addBtn);

                for (int i = 0; i < rnd.outcomes.Count; i++)
                {
                    int idx = i;
                    var box = CreateOptionBox();

                    var header = new VisualElement() { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginBottom = 5 } };
                    header.Add(new Label($"Исход {i + 1}") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                    
                    var del = new Button(() => { 
                        rnd.outcomes.RemoveAt(idx); 
                        EditorUtility.SetDirty(rnd); 
                        PopulateView(_graph); 
                    }) { text = "X" };
                    del.style.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
                    header.Add(del);
                    box.Add(header);

                    var row = new VisualElement() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                    row.Add(new Label("Вес (Chance):") { style = { width = 90 } });
                    
                    var chanceField = new FloatField() { value = rnd.outcomes[i].chance };
                    chanceField.style.flexGrow = 1;
                    chanceField.RegisterValueChangedCallback(e => { 
                        rnd.outcomes[idx].chance = e.newValue; 
                        EditorUtility.SetDirty(rnd); 
                    });
                    row.Add(chanceField);
                    box.Add(row);

                    var port = GeneratePort(nodeView, Direction.Output, Port.Capacity.Single);
                    port.userData = idx;
                    port.portName = "->"; 
                    port.style.alignSelf = Align.FlexEnd;
                    port.style.marginTop = 5;
                    box.Add(port);

                    nodeView.extensionContainer.Add(box);
                }
            }

            nodeView.RefreshExpandedState();
            nodeView.RefreshPorts();
            
            nodeView.RegisterCallback<MouseUpEvent>(e => { nodeData.graphPosition = nodeView.GetPosition(); EditorUtility.SetDirty(nodeData); });
            nodeView.RegisterCallback<MouseDownEvent>(e => Selection.activeObject = nodeData);
            AddElement(nodeView);
        }

        private void RestoreConnections(DialogueNode nodeData)
        {
            var outputNodeView = GetNodeByGuid(nodeData.id) as Node;
            if (outputNodeView == null) return;

            DialogueNode nextNode = null;
            if (nodeData is StartNode s) nextNode = s.nextNode;
            else if (nodeData is PhraseNode p) nextNode = p.nextNode;
            else if (nodeData is EventNode e) nextNode = e.nextNode;

            if (nextNode != null)
            {
                var targetNodeView = GetNodeByGuid(nextNode.id);
                if (outputNodeView.outputContainer.childCount > 0 && targetNodeView != null)
                {
                    var outPort = outputNodeView.outputContainer[0] as Port;
                    var inPort = targetNodeView.inputContainer[0] as Port;
                    LinkNodes(outPort, inPort);
                }
            }

            if (nodeData is ChoiceNode choice)
            {
                var allPorts = outputNodeView.Query<Port>().ToList();
                var outputPorts = allPorts.Where(p => p.direction == Direction.Output).ToList();

                for (int i = 0; i < choice.options.Count; i++)
                {
                    if (choice.options[i].nextNode == null) continue;
                    var targetNodeView = GetNodeByGuid(choice.options[i].nextNode.id);
                    if (targetNodeView == null) continue;
                    var outPort = outputPorts.FirstOrDefault(p => p.userData is int idx && idx == i);
                    var inPort = targetNodeView.inputContainer[0] as Port;
                    LinkNodes(outPort, inPort);
                }
            }

            // --- ДОБАВЛЕНО: RANDOM NODE CONNECTIONS ---
            if (nodeData is RandomNode rnd)
            {
                var allPorts = outputNodeView.Query<Port>().ToList();
                var outputPorts = allPorts.Where(p => p.direction == Direction.Output).ToList();

                for (int i = 0; i < rnd.outcomes.Count; i++)
                {
                    if (rnd.outcomes[i].nextNode == null) continue;
                    
                    var targetNodeView = GetNodeByGuid(rnd.outcomes[i].nextNode.id);
                    if (targetNodeView == null) continue;

                    var outPort = outputPorts.FirstOrDefault(p => p.userData is int idx && idx == i);
                    var inPort = targetNodeView.inputContainer[0] as Port;
                    LinkNodes(outPort, inPort);
                }
            }
        }

        private VisualElement CreateOptionBox()
        {
            var box = new VisualElement();
            box.style.backgroundColor = new Color(0,0,0,0.3f);
            Color col = new Color(1,1,1,0.1f);
            box.style.borderTopColor = col; box.style.borderBottomColor = col;
            box.style.borderLeftColor = col; box.style.borderRightColor = col;
            box.style.borderTopWidth = 1; box.style.borderBottomWidth = 1; 
            box.style.borderLeftWidth = 1; box.style.borderRightWidth = 1;
            box.style.marginBottom = 5; box.style.paddingTop = 5; box.style.paddingBottom = 5; 
            box.style.paddingLeft = 5; box.style.paddingRight = 5;
            return box;
        }

        private void AddTextField(Node node, string label, string val, Action<string> onChg, ScriptableObject data, bool multi=false)
        {
            var f = new TextField(label) { value = val, multiline = multi };
            f.RegisterValueChangedCallback(e => { onChg(e.newValue); EditorUtility.SetDirty(data); });
            if(multi) f.style.whiteSpace = WhiteSpace.Normal;
            node.extensionContainer.Add(f);
        }

        private Port GeneratePort(Node node, Direction dir, Port.Capacity capacity) => node.InstantiatePort(Orientation.Horizontal, dir, capacity, typeof(float));
        private void LinkNodes(Port output, Port input) { if(output != null && input != null) AddElement(output.ConnectTo(input)); }
        private Node GetNodeByGuid(string id) => nodes.ToList().FirstOrDefault(n => n.viewDataKey == id);
        public override List<Port> GetCompatiblePorts(Port start, NodeAdapter adapter) => ports.ToList().Where(p => start != p && start.node != p.node && start.direction != p.direction).ToList();

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.elementsToRemove != null)
            {
                foreach (var el in change.elementsToRemove)
                {
                    if (el is Node n) { var d = _graph.allNodes.FirstOrDefault(x => x.id == n.viewDataKey); if(d) _graph.DeleteNode(d); }
                    if (el is Edge e) RemoveLink(e);
                }
            }
            if (change.edgesToCreate != null) foreach (var e in change.edgesToCreate) CreateLink(e);
            
            if (change.movedElements != null)
            {
                foreach (var element in change.movedElements)
                {
                    if (element is Node nodeView)
                    {
                        var nodeData = _graph.allNodes.FirstOrDefault(n => n.id == nodeView.viewDataKey);
                        if (nodeData != null)
                        {
                            nodeData.graphPosition = nodeView.GetPosition();
                            EditorUtility.SetDirty(nodeData);
                        }
                    }
                }
            }

            return change;
        }

        private void CreateLink(Edge e)
        {
            var inDat = _graph.allNodes.FirstOrDefault(n => n.id == e.input.node.viewDataKey);
            var outDat = _graph.allNodes.FirstOrDefault(n => n.id == e.output.node.viewDataKey);
            
            if(outDat is StartNode s) s.nextNode = inDat;
            if(outDat is PhraseNode p) p.nextNode = inDat;
            if(outDat is EventNode ev) ev.nextNode = inDat;
            if(outDat is ChoiceNode c) { int i = (int)e.output.userData; if(i < c.options.Count) c.options[i].nextNode = inDat; }
            // --- RANDOM LINK ---
            if(outDat is RandomNode rnd) { int i = (int)e.output.userData; if(i < rnd.outcomes.Count) rnd.outcomes[i].nextNode = inDat; }
            
            EditorUtility.SetDirty(outDat);
        }

        private void RemoveLink(Edge e)
        {
            if(e.output?.node == null) return;
            var outDat = _graph.allNodes.FirstOrDefault(n => n.id == e.output.node.viewDataKey);
            if(outDat == null) return;

            if (outDat is StartNode s) s.nextNode = null;
            if (outDat is PhraseNode p) p.nextNode = null;
            if (outDat is EventNode ev) ev.nextNode = null;
            if (outDat is ChoiceNode c) { int i = (int)e.output.userData; if(i < c.options.Count) c.options[i].nextNode = null; }
            // --- RANDOM UNLINK ---
            if (outDat is RandomNode rnd) { int i = (int)e.output.userData; if(i < rnd.outcomes.Count) rnd.outcomes[i].nextNode = null; }
            
            EditorUtility.SetDirty(outDat);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            Vector2 pos = viewTransform.matrix.inverse.MultiplyPoint(evt.localMousePosition);
            evt.menu.AppendAction("Создать СТАРТ", _ => Create<StartNode>(pos));
            evt.menu.AppendAction("Создать Фразу", _ => Create<PhraseNode>(pos));
            evt.menu.AppendAction("Создать Выбор", _ => Create<ChoiceNode>(pos));
            evt.menu.AppendAction("Создать Событие", _ => Create<EventNode>(pos));
            evt.menu.AppendAction("Создать Случайность", _ => Create<RandomNode>(pos)); // Добавлено
            evt.menu.AppendAction("Создать КОНЕЦ", _ => Create<EndNode>(pos));
        }

        private void Create<T>(Vector2 pos) where T : DialogueNode
        {
            var node = _graph.CreateNode<T>();
            node.graphPosition = new Rect(pos, Vector2.zero);
            if(node is ChoiceNode c) c.options.Add(new ChoiceNode.ChoiceOption { text = "Далее..." });
            // Добавляем дефолтные исходы для рандома
            if(node is RandomNode r) { 
                r.outcomes.Add(new RandomNode.RandomOutcome { chance = 1f });
                r.outcomes.Add(new RandomNode.RandomOutcome { chance = 1f });
            }
            CreateNodeView(node);
        }
    }
}