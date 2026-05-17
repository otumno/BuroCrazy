// === FILE: Assets/Scripts/Cinematic/Editor/CinematicEditorWindow.cs ===
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace CinematicSystem.Editor
{
    /// <summary>
    /// Главное окно редактора CinematicGraph.
    /// Открывается через Tools -> Cinematic Graph Editor.
    /// </summary>
    public class CinematicEditorWindow : EditorWindow
    {
        private CinematicGraphView graphView;
        private CinematicGraph currentGraph;
        private ObjectField graphField;
        private ToolbarMenu nodeTypeMenu;
        
        [MenuItem("Tools/Cinematic Graph Editor")]
        public static void OpenWindow(CinematicGraph graph = null)
        {
            var window = GetWindow<CinematicEditorWindow>();
            window.titleContent = new GUIContent("Cinematic Graph Editor");
            window.minSize = new Vector2(800, 600);
            
            if (graph != null)
            {
                window.currentGraph = graph;
                window.LoadGraph();
            }
        }

        private void OnEnable()
        {
            // Создаём корневой элемент
            var root = rootVisualElement;
            
            // Загружаем UXML-шаблон (если есть) или создаём программно
            CreateUI(root);
        }

        private void CreateUI(VisualElement root)
        {
            // Toolbar
            var toolbar = new Toolbar();
            
            // Поле выбора графа
            graphField = new ObjectField("Graph")
            {
                objectType = typeof(CinematicGraph),
                allowSceneObjects = false
            };
            graphField.RegisterValueChangedCallback(evt => 
            {
                currentGraph = evt.newValue as CinematicGraph;
                LoadGraph();
            });
            toolbar.Add(graphField);
            
            // Кнопки
            var saveButton = new ToolbarButton(() => SaveGraph()) { text = "Save" };
            var loadJsonButton = new ToolbarButton(() => ImportJSON()) { text = "Import JSON" };
            var pasteJsonButton = new ToolbarButton(() => ImportFromClipboard()) { text = "Paste JSON" };
            var exportJsonButton = new ToolbarButton(() => ExportJSON()) { text = "Export JSON" };
            var autoLayoutButton = new ToolbarButton(() => AutoLayout()) { text = "Auto Layout" };
            
            toolbar.Add(saveButton);
            toolbar.Add(loadJsonButton);
            toolbar.Add(pasteJsonButton);
            toolbar.Add(exportJsonButton);
            toolbar.Add(autoLayoutButton);
            
            root.Add(toolbar);
            
            // GraphView занимает всё оставшееся пространство
            graphView = new CinematicGraphView(this);
            graphView.StretchToParentSize();
            graphView.style.top = 22; // Под тулбаром
            
            root.Add(graphView);
        }

        /// <summary>
        /// Загрузить граф в редактор.
        /// </summary>
        public void LoadGraph()
        {
            if (currentGraph != null)
            {
                graphView.PopulateView(currentGraph);
            }
            else
            {
                graphView.ClearGraph();
            }
        }

        /// <summary>
        /// Сохранить граф.
        /// </summary>
        private void SaveGraph()
        {
            if (currentGraph == null) return;
            
            EditorUtility.SetDirty(currentGraph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"[CinematicEditor] Сохранён граф: {currentGraph.name}");
        }

        /// <summary>
        /// Импорт из JSON.
        /// </summary>
        private void ImportJSON()
        {
            string path = EditorUtility.OpenFilePanel("Import Cinematic Graph JSON", "Assets", "json");
            if (string.IsNullOrEmpty(path)) return;
            
            string json = System.IO.File.ReadAllText(path);
            
            // Создаём новый граф
            var newGraph = CinematicJSONImporter.Import(json, this);
            if (newGraph != null)
            {
                // Выполняем авто-расстановку узлов
                CinematicJSONImporter.AutoLayoutNodes(newGraph);
                
                currentGraph = newGraph;
                graphField.SetValueWithoutNotify(newGraph);
                LoadGraph();
                
                // После загрузки выполняем AutoArrange в GraphView
                graphView.AutoArrange();
                
                // Автосохранение в Assets/Data/CinematicGraphs/
                string folderPath = "Assets/Data/CinematicGraphs";
                if (!System.IO.Directory.Exists(folderPath))
                {
                    System.IO.Directory.CreateDirectory(folderPath);
                }
                
                string fileName = SanitizeFileName(newGraph.graphName) + ".asset";
                string assetPath = folderPath + "/" + fileName;
                
                AssetDatabase.CreateAsset(newGraph, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                // Открываем содержимое папки в Project (PingObject на asset откроет папку с его местоположением)
                var savedAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (savedAsset != null)
                {
                    EditorGUIUtility.PingObject(savedAsset);
                    Selection.activeObject = savedAsset;
                }
                
                Debug.Log($"[CinematicEditor] Импортирован и сохранён граф: {newGraph.graphName} в {assetPath}");
            }
        }

        /// <summary>
        /// Экспорт в JSON.
        /// </summary>
        private void ExportJSON()
        {
            if (currentGraph == null) return;
            
            string json = CinematicJSONExporter.Export(currentGraph);
            string path = EditorUtility.SaveFilePanel(
                "Export Cinematic Graph JSON",
                "Assets",
                currentGraph.graphName + ".json",
                "json");
            
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                Debug.Log($"[CinematicEditor] Экспортирован граф: {currentGraph.graphName}");
            }
        }

        /// <summary>
        /// Импорт JSON из буфера обмена.
        /// </summary>
        private void ImportFromClipboard()
        {
            string json = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(json))
            {
                EditorUtility.DisplayDialog("Ошибка", "Буфер обмена пуст", "OK");
                return;
            }
            
            var newGraph = CinematicJSONImporter.Import(json, this);
            if (newGraph != null)
            {
                // Выполняем авто-расстановку узлов
                CinematicJSONImporter.AutoLayoutNodes(newGraph);
                
                currentGraph = newGraph;
                graphField.SetValueWithoutNotify(newGraph);
                LoadGraph();
                
                // После загрузки выполняем AutoArrange в GraphView
                graphView.AutoArrange();
                
                // Автосохранение в Assets/Data/CinematicGraphs/
                string folderPath = "Assets/Data/CinematicGraphs";
                if (!System.IO.Directory.Exists(folderPath))
                {
                    System.IO.Directory.CreateDirectory(folderPath);
                }
                
                string fileName = SanitizeFileName(newGraph.graphName) + ".asset";
                string assetPath = folderPath + "/" + fileName;
                
                AssetDatabase.CreateAsset(newGraph, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                // Открываем содержимое папки в Project
                var savedAsset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                if (savedAsset != null)
                {
                    EditorGUIUtility.PingObject(savedAsset);
                    Selection.activeObject = savedAsset;
                }
                
                Debug.Log($"[CinematicEditor] Импортирован и сохранён граф: {newGraph.graphName} в {assetPath}");
            }
        }
        
        /// <summary>
        /// Очистка имени файла от недопустимых символов.
        /// </summary>
        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Graph";
            // Убираем символы, недопустимые в именах файлов
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            string result = name;
            foreach (char c in invalidChars)
            {
                result = result.Replace(c, '_');
            }
            return result;
        }

        /// <summary>
        /// Автоматическая расстановка узлов.
        /// </summary>
        private void AutoLayout()
        {
            graphView.AutoArrange();
        }

        /// <summary>
        /// Получить текущий граф.
        /// </summary>
        public CinematicGraph GetCurrentGraph() => currentGraph;
    }
}