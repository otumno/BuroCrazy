// Файл: Assets/Scripts/DialogueSystem/Editor/DialogueEditorWindow.cs
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements; // Для ToolbarButton
using DialogueSystem.Data;

namespace DialogueSystem.Editor
{
    public class DialogueEditorWindow : EditorWindow
    {
        private DialogueGraphView _graphView;
        private DialogueGraph _currentGraph;

        [OnOpenAsset(1)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            if (Selection.activeObject is DialogueGraph)
            {
                OpenWindow((DialogueGraph)Selection.activeObject);
                return true;
            }
            return false;
        }

        public static void OpenWindow(DialogueGraph graph)
        {
            var window = GetWindow<DialogueEditorWindow>();
            window.titleContent = new GUIContent("Dialogue Editor");
            window.LoadGraph(graph);
        }

        private void OnEnable()
        {
            ConstructGraphView();
            GenerateToolbar();
            if (_currentGraph != null) LoadGraph(_currentGraph);
        }

        private void OnDisable()
        {
            if (_graphView != null) rootVisualElement.Remove(_graphView);
        }

        private void ConstructGraphView()
        {
            _graphView = new DialogueGraphView(this)
            {
                name = "Dialogue Graph"
            };
            _graphView.StretchToParentSize();
            rootVisualElement.Add(_graphView);
        }

        private void GenerateToolbar()
        {
            var toolbar = new Toolbar();

            // Кнопка сохранения
            var saveButton = new ToolbarButton(() => SaveGraph()) { text = "Save Asset" };
            toolbar.Add(saveButton);

            // Кнопка поиска в проекте
            var pingButton = new ToolbarButton(() => { if(_currentGraph) EditorGUIUtility.PingObject(_currentGraph); }) { text = "Show in Project" };
            toolbar.Add(pingButton);

            // --- НОВАЯ КНОПКА СПРАВКИ ---
            var helpButton = new ToolbarButton(() => _graphView.ToggleHelp()) { text = "Справка / Help" };
            // Делаем её чуть заметнее (опционально)
            helpButton.style.unityFontStyleAndWeight = FontStyle.Bold; 
            toolbar.Add(helpButton);
            // -----------------------------

            rootVisualElement.Add(toolbar);
        }

        public void LoadGraph(DialogueGraph graph)
        {
            _currentGraph = graph;
            if (_graphView != null) _graphView.PopulateView(graph);
        }

        public void SaveGraph()
        {
            if (_currentGraph == null) return;
            EditorUtility.SetDirty(_currentGraph);
            AssetDatabase.SaveAssets();
        }
    }
}