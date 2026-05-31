// === FILE: Assets/Scripts/Cinematic/Editor/CinematicGraphView.cs ===
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace CinematicSystem.Editor
{
    /// <summary>
    /// GraphView для визуального редактирования CinematicGraph.
    /// </summary>
    public class CinematicGraphView : GraphView
    {
        private CinematicEditorWindow editorWindow;
        private CinematicGraph graph;
        private Dictionary<string, CinematicNodeView> nodeViews = new Dictionary<string, CinematicNodeView>();

        public CinematicGraphView(CinematicEditorWindow window)
        {
            editorWindow = window;
            
            // Добавляем манипуляторы
            this.AddManipulator(new ContentZoomer());
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            
            // Стили (опционально)
            var styleSheet = Resources.Load<StyleSheet>("CinematicGraphStyles");
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }
            
            // Обработка контекстного меню
            RegisterCallback<ContextualMenuPopulateEvent>(OnContextMenuPopulate);
            
            // Обработка создания связей
            graphViewChanged = OnGraphViewChanged;
        }

        /// <summary>
        /// Заполнить представление графом.
        /// </summary>
        public void PopulateView(CinematicGraph cinematicGraph)
        {
            graph = cinematicGraph;
            ClearGraph();
            
            if (graph == null) return;
            
            // Визуальная индикация фонового графа
            if (graph.isBackground)
            {
                style.backgroundColor = new Color(0.15f, 0.12f, 0.2f, 1f);
            }
            else
            {
                style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            }
            
            // Создаём узлы
            foreach (var node in graph.allNodes)
            {
                if (node == null) continue;
                CreateNodeView(node);
            }
            
            // Создаём связи
            foreach (var node in graph.allNodes)
            {
                if (node == null) continue;
                CreateEdgesForNode(node);
            }
        }

        /// <summary>
        /// Очистить граф.
        /// </summary>
        public void ClearGraph()
        {
            nodeViews.Clear();
            
            // Удаляем все элементы
            var nodesToRemove = nodes.ToList();
            foreach (var node in nodesToRemove)
            {
                RemoveElement(node);
            }
            
            var edgesToRemove = edges.ToList();
            foreach (var edge in edgesToRemove)
            {
                RemoveElement(edge);
            }
        }

        /// <summary>
        /// Создать визуальное представление узла.
        /// </summary>
        private void CreateNodeView(CinematicNode node)
        {
            var nodeView = new CinematicNodeView(node, this);
            nodeView.SetPosition(new Rect(node.editorPosition.x, node.editorPosition.y, 200, 150));
            nodeViews[node.id] = nodeView;
            AddElement(nodeView);
        }

        /// <summary>
        /// Создать связи для узла.
        /// </summary>
        private void CreateEdgesForNode(CinematicNode node)
        {
            if (!nodeViews.TryGetValue(node.id, out var fromView)) return;
            
            // Связь для StartNode (у него есть nextNode)
            if (node is Nodes.StartNode startNode && startNode.nextNode != null)
            {
                CreateEdgeToNode(fromView, startNode.nextNode);
            }
            
            // Связь для NextNode
            if (node is NextNode nextNode && nextNode.nextNode != null)
            {
                CreateEdgeToNode(fromView, nextNode.nextNode);
            }
            
            // Связи для ConditionNode
            if (node is Nodes.ConditionNode condNode)
            {
                if (condNode.trueNode != null)
                    CreateEdgeToNode(fromView, condNode.trueNode, "true");
                if (condNode.falseNode != null)
                    CreateEdgeToNode(fromView, condNode.falseNode, "false");
            }
            
            // Связи для RandomNode
            if (node is Nodes.RandomNode randNode && randNode.outcomes != null)
            {
                for (int i = 0; i < randNode.outcomes.Count; i++)
                {
                    var outcome = randNode.outcomes[i];
                    if (outcome.nextNode != null)
                    {
                        string portName = i.ToString();
                        CreateEdgeToNode(fromView, outcome.nextNode, portName);
                    }
                }
            }
        }

        /// <summary>
        /// Создать связь к узлу.
        /// </summary>
        private void CreateEdgeToNode(CinematicNodeView fromView, CinematicNode toNode, string portName = "")
        {
            if (!nodeViews.TryGetValue(toNode.id, out var toView)) return;
            
            var edge = new Edge
            {
                output = fromView.OutputPort,
                input = toView.GetInputPort(portName)
            };
            
            edge.output.Connect(edge);
            edge.input.Connect(edge);
            
            AddElement(edge);
        }

        /// <summary>
        /// Контекстное меню для создания узлов.
        /// </summary>
        private void OnContextMenuPopulate(ContextualMenuPopulateEvent evt)
        {
            if (graph == null) return;
            
            Vector2 localMousePos = evt.localMousePosition;
            
            // Добавляем пункты меню для каждого типа узла
            var nodeTypes = new[]
            {
                ("Start", typeof(Nodes.StartNode)),
                ("Move To", typeof(Nodes.MoveToNode)),
                ("Teleport", typeof(Nodes.TeleportNode)),
                ("Say Bubble", typeof(Nodes.SayBubbleNode)),
                ("Say Dialog", typeof(Nodes.SayDialogNode)),
                ("Call Dialogue", typeof(Nodes.CallDialogueNode)),
                ("Call Cinematic Graph", typeof(Nodes.CallCinematicGraphNode)),
                ("Camera", typeof(Nodes.CameraNode)),
                ("Camera Move", typeof(Nodes.CameraMoveNode)),
                ("Wait For Seconds", typeof(Nodes.WaitForSecondsNode)),
                ("Wait For UI Click", typeof(Nodes.WaitForUIClickNode)),
                ("Wait For Despawn", typeof(Nodes.WaitForCharacterDespawnNode)),
                ("Condition", typeof(Nodes.ConditionNode)),
                ("Random", typeof(Nodes.RandomNode)),
                ("Event", typeof(Nodes.EventNode)),
                ("Spawn Character", typeof(Nodes.SpawnCharacterNode)),
                ("Comment", typeof(Nodes.CommentNode)),
                ("End", typeof(Nodes.EndNode))
            };
            
            foreach (var (name, type) in nodeTypes)
            {
                int capturedName = name.IndexOf(' ');
                string displayName = capturedName > 0 ? name.Substring(0, capturedName) : name;
                
                evt.menu.AppendAction($"Create Node/{name}", action => 
                {
                    AddNode(type, localMousePos);
                });
            }
        }

        /// <summary>
        /// Добавить новый узел в указанной позиции.
        /// После создания узел выделяется и прокручивается в центр внимания.
        /// </summary>
        /// <param name="nodeType">Тип создаваемого узла</param>
        /// <param name="position">Позиция на graph view</param>
        public void AddNode(System.Type nodeType, Vector2 position)
        {
            if (graph == null) return;
            
            // Создаём узел через Graph
            var node = graph.CreateNode(nodeType);
            node.editorPosition = new Rect(position.x, position.y, 200, 150);
            
            // Создаём визуальное представление
            var nodeView = CreateNodeViewAndReturn(node);
            
            // Выделяем созданный узел и прокручиваем к нему
            ClearSelection();
            AddToSelection(nodeView);
            FrameSelection();
            
            // Подсвечиваем узел (ping effect)
            var nodeAsset = nodeView.Node as UnityEngine.Object;
            if (nodeAsset != null) EditorGUIUtility.PingObject(nodeAsset);
        }
        
        /// <summary>
        /// Создать визуальное представление узла и вернуть его.
        /// </summary>
        private CinematicNodeView CreateNodeViewAndReturn(CinematicNode node)
        {
            var nodeView = new CinematicNodeView(node, this);
            nodeView.SetPosition(new Rect(node.editorPosition.x, node.editorPosition.y, 200, 150));
            nodeViews[node.id] = nodeView;
            AddElement(nodeView);
            return nodeView;
        }

        /// <summary>
        /// Обработка изменений в графе.
        /// </summary>
        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            // Обработка удаления узлов
            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    if (element is CinematicNodeView nodeView)
                    {
                        graph.DeleteNode(nodeView.Node);
                        nodeViews.Remove(nodeView.Node.id);
                    }
                    
                    if (element is Edge edge)
                    {
                        var fromView = edge.output.node as CinematicNodeView;
                        var toView = edge.input.node as CinematicNodeView;
                        
                        if (fromView != null && toView != null)
                        {
                            // Определяем какой порт
                            if (fromView.Node is NextNode)
                            {
                                (fromView.Node as NextNode).nextNode = null;
                            }
                        }
                    }
                }
            }
            
            // Обработка создания связей
            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    var fromView = edge.output.node as CinematicNodeView;
                    var toView = edge.input.node as CinematicNodeView;
                    
                    if (fromView != null && toView != null)
                    {
                        // Определяем тип связи
                        if (fromView.Node is NextNode nextNode)
                        {
                            nextNode.nextNode = toView.Node;
                        }
                        
                        // Обработка связи для RandomNode
                        if (fromView.Node is Nodes.RandomNode randNode)
                        {
                            string portName = edge.output.portName;
                            if (int.TryParse(portName, out int index) && randNode.outcomes != null)
                            {
                                // Расширяем список если нужно
                                while (randNode.outcomes.Count <= index)
                                {
                                    randNode.outcomes.Add(new Nodes.RandomOutcome());
                                }
                                randNode.outcomes[index].nextNode = toView.Node;
                            }
                        }
                    }
                }
            }
            
            // Сохраняем позиции узлов
            if (change.movedElements != null)
            {
                foreach (var element in change.movedElements)
                {
                    if (element is CinematicNodeView nodeView)
                    {
                        Rect pos = nodeView.GetPosition();
                        pos.width = 200;
                        pos.height = 150;
                        nodeView.Node.editorPosition = pos;
                    }
                }
            }
            
            return change;
        }

        /// <summary>
        /// Автоматическая расстановка узлов по дереву графа.
        /// Использует DFS для обхода и расставляет узлы с отступами:
        /// - x увеличивается на 300 для каждого уровня
        /// - ConditionNode: true-ветвь вниз (+200 по Y), false-ветвь вверх (-200 по Y)
        /// </summary>
        public void AutoArrange()
        {
            if (graph == null || graph.startNode == null) return;
            
            var visited = new HashSet<string>();
            float startX = 50;
            float startY = 50;
            float nodeWidth = 200;
            float nodeHeight = 150;
            float xOffset = 300; // Горизонтальный отступ
            float yOffsetBranch = 200; // Вертикальный отступ для ветвей
            
            // DFS обход графа
            void ArrangeNode(CinematicNode node, float x, float y)
            {
                if (node == null || visited.Contains(node.id)) return;
                visited.Add(node.id);
                
                if (nodeViews.TryGetValue(node.id, out var view))
                {
                    Rect pos = new Rect(x, y, nodeWidth, nodeHeight);
                    view.SetPosition(pos);
                    node.editorPosition = pos;
                }
                
                // Следующий узел (линейная цепочка)
                if (node is NextNode nextNode && nextNode.nextNode != null)
                {
                    ArrangeNode(nextNode.nextNode, x, y + nodeHeight + 50);
                }
                
                // Узлы с условием - ветвление
                if (node is Nodes.ConditionNode condNode)
                {
                    // True ветвь - вниз
                    if (condNode.trueNode != null && !visited.Contains(condNode.trueNode.id))
                    {
                        ArrangeNode(condNode.trueNode, x + xOffset, y + yOffsetBranch);
                    }
                    
                    // False ветвь - вверх
                    if (condNode.falseNode != null && !visited.Contains(condNode.falseNode.id))
                    {
                        ArrangeNode(condNode.falseNode, x + xOffset, y - yOffsetBranch);
                    }
                }
                
                // CallDialogueNode - может иметь следующий узел
                if (node is Nodes.CallDialogueNode callNode)
                {
                    var next = GetNextNode(callNode);
                    if (next != null && !visited.Contains(next.id))
                    {
                        ArrangeNode(next, x, y + nodeHeight + 50);
                    }
                }
            }
            
            ArrangeNode(graph.startNode, startX, startY);
            
            // После расстановки выделяем все узлы и прокручиваем к началу
            ClearSelection();
            foreach (var view in nodeViews.Values)
            {
                AddToSelection(view);
            }
            FrameSelection();
        }
        
        /// <summary>
        /// Получить следующий узел для узла (вспомогательный метод).
        /// </summary>
        private CinematicNode GetNextNode(CinematicNode node)
        {
            if (node is NextNode next) return next.nextNode;
            return null;
        }

        /// <summary>
        /// Получить представление узла по ID.
        /// </summary>
        public CinematicNodeView GetNodeView(string id)
        {
            nodeViews.TryGetValue(id, out var view);
            return view;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            
            ports.ForEach(port =>
            {
                if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
                {
                    compatiblePorts.Add(port);
                }
            });
            
            return compatiblePorts;
        }
    }
}