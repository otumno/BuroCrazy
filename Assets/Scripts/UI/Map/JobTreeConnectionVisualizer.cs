// Assets/Scripts/UI/Map/JobTreeConnectionVisualizer.cs
// Визуализатор связей между нодами дерева карьеры.
// Рисует линии со стрелками в Scene View от parent -> child по jobData.requiredPreviousJob.
//
// Проблема: TreeZone находится на UI Canvas (Overlay или Camera).
// Gizmos.DrawLine в OnDrawGizmos НЕ рисует для UI в Screen Space - Overlay.
// Решение: подписываемся на SceneView.duringSceneGui и используем Handles API,
// который корректно работает в обоих режимах.

using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UI.Map
{
    /// <summary>
    /// Визуализатор связей между нодами дерева карьеры.
    /// Поместите этот компонент на Transform, который содержит (или является родителем)
    /// всех JobNodeUI — обычно это "TreeZone".
    /// </summary>
    [ExecuteAlways]
    public class JobTreeConnectionVisualizer : MonoBehaviour
    {
        [Header("Настройки линий")]
        [SerializeField] private bool drawConnections = true;
        [SerializeField] private Color lineColor = new Color(0.2f, 0.5f, 1f, 0.8f); // синий
        [SerializeField] private float lineThickness = 3f;
        [SerializeField] private bool drawArrows = true;
        [Tooltip("Длина наконечника стрелки в пикселях (Screen Space).")]
        [SerializeField] private float arrowHeadLength = 18f;
        [Tooltip("Ширина наконечника стрелки в пикселях (Screen Space).")]
        [SerializeField] private float arrowHeadWidth = 10f;
        [Tooltip("Позиция стрелки вдоль линии: 0 = у родителя, 1 = у ребёнка.")]
        [Range(0.2f, 0.95f)]
        [SerializeField] private float arrowPositionT = 0.7f;

        [Header("Опции")]
        [Tooltip("Скрывать визуализацию в Play Mode.")]
        [SerializeField] private bool hideInPlayMode = true;
        [Tooltip("Искать JobNodeUI во всём поддереве (true) или только на прямых детях (false).")]
        [SerializeField] private bool searchRecursive = true;

        // Кэш
        private readonly List<JobNodeUI> _nodeCache = new List<JobNodeUI>(32);

#if UNITY_EDITOR
        private void OnEnable()
        {
            // Подписываемся на SceneView.duringSceneGui — это работает для UI в Screen Space - Overlay
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        // Вызывается при отрисовке Scene View
        private void OnSceneGUI(SceneView sceneView)
        {
            DrawAll();
        }

        // Для обратной совместимости — рисуем и в OnDrawGizmos (для не-overlay сцен)
        private void OnDrawGizmos()
        {
            // На самом деле мы рисуем через OnSceneGUI, но если Gizmos выключены
            // в Editor, мы всё равно хотим нарисовать в Scene View (через Handles).
            DrawAll();
        }
#else
        private void OnEnable() { }
        private void OnDisable() { }
#endif

        // Общая логика рисования — вызывается из OnSceneGUI и OnDrawGizmos
        private void DrawAll()
        {
            if (!drawConnections) return;
            if (hideInPlayMode && Application.isPlaying) return;

            // Собираем все JobNodeUI в поддереве
            _nodeCache.Clear();
            if (searchRecursive)
            {
                GetComponentsInChildren(includeInactive: true, _nodeCache);
            }
            else
            {
                foreach (Transform child in transform)
                {
                    var node = child.GetComponent<JobNodeUI>();
                    if (node != null) _nodeCache.Add(node);
                }
            }

            if (_nodeCache.Count == 0) return;

            // Карта jobID -> JobNodeUI
            var map = new Dictionary<string, JobNodeUI>(_nodeCache.Count);
            for (int i = 0; i < _nodeCache.Count; i++)
            {
                var n = _nodeCache[i];
                if (n == null || n.jobData == null) continue;
                if (!string.IsNullOrEmpty(n.jobData.jobID))
                    map[n.jobData.jobID] = n;
            }

#if UNITY_EDITOR
            // Сохраняем Handles.color и сбрасываем после
            Color prevHandlesColor = Handles.color;
            Handles.color = lineColor;

            Vector3 a, b, dir;
            for (int i = 0; i < _nodeCache.Count; i++)
            {
                var n = _nodeCache[i];
                if (n == null || n.jobData == null) continue;
                var parentJob = n.jobData.requiredPreviousJob;
                if (parentJob == null) continue;
                if (string.IsNullOrEmpty(parentJob.jobID)) continue;

                if (!map.TryGetValue(parentJob.jobID, out var parentNode)) continue;
                if (parentNode == null || parentNode.transform == n.transform) continue;

                a = parentNode.transform.position;
                b = n.transform.position;
                dir = b - a;
                float distance = dir.magnitude;

                if (distance < 0.01f)
                {
                    // Если parent и child в одной точке, рисуем маленький кружок для обозначения
                    Handles.DrawWireDisc(a, Vector3.forward, 10f);
                    continue;
                }

                // Линия (Handles работает с UI в Overlay)
                Handles.DrawAAPolyLine(lineThickness, a, b);

                // Стрелка
                if (drawArrows)
                {
                    Vector3 normalized = dir / distance;
                    // Перпендикуляр в плоскости XY
                    Vector3 perp = new Vector3(-normalized.y, normalized.x, 0f);
                    Vector3 arrowBase = Vector3.Lerp(a, b, arrowPositionT);
                    Vector3 arrowTip = arrowBase + normalized * arrowHeadLength;
                    Vector3 leftBase = arrowBase + perp * (arrowHeadWidth * 0.5f);
                    Vector3 rightBase = arrowBase - perp * (arrowHeadWidth * 0.5f);
                    Vector3 tailBase = arrowBase - normalized * (arrowHeadLength * 0.3f);

                    Handles.DrawAAPolyLine(lineThickness, arrowTip, leftBase);
                    Handles.DrawAAPolyLine(lineThickness, arrowTip, rightBase);
                    Handles.DrawAAPolyLine(lineThickness, arrowTip, tailBase);
                }
            }

            Handles.color = prevHandlesColor;
#else
            // Runtime: используем Gizmos (только в build сценах не-overlay)
            Color prevGizmosColor = Gizmos.color;
            Gizmos.color = lineColor;
            for (int i = 0; i < _nodeCache.Count; i++)
            {
                var n = _nodeCache[i];
                if (n == null || n.jobData == null) continue;
                var parentJob = n.jobData.requiredPreviousJob;
                if (parentJob == null) continue;
                if (!map.TryGetValue(parentJob.jobID, out var parentNode)) continue;
                if (parentNode == null) continue;

                Gizmos.DrawLine(parentNode.transform.position, n.transform.position);
            }
            Gizmos.color = prevGizmosColor;
#endif
        }
    }
}
