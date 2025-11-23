using UnityEditor;
using UnityEngine;
using Utilities;

namespace Editor
{
    [CustomEditor(typeof(GraphBuilder))]
    public class GraphBuilderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Сначала рисуем стандартные поля (maxDistance, pathRadius и т.д.)
            DrawDefaultInspector();

            // Получаем ссылку на наш скрипт
            GraphBuilder builder = (GraphBuilder)target;

            // Рисуем большую, удобную кнопку
            if (GUILayout.Button("Перестроить Граф", GUILayout.Height(30)))
            {
                builder.BuildGraph();
            }
        }
    }
}