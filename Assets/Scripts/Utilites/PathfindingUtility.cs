// НОВЫЙ ФАЙЛ: Utilites/PathfindingUtility.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class PathfindingUtility
{
    // Мы берем самую надежную версию BuildPathTo и делаем ее универсальной
    public static Queue<Waypoint> BuildPathTo(Vector2 startPos, Vector2 targetPos, GameObject self)
    {
        var path = new Queue<Waypoint>();
        var allWaypoints = Object.FindObjectsByType<Waypoint>(FindObjectsSortMode.None);

        if (allWaypoints == null || allWaypoints.Length == 0)
        {
            Debug.LogError("[PathfindingUtility] На сцене не найдено ни одного вейпоинта!");
            return path;
        }

        // Находим ближайшие к старту и финишу точки (самый надежный способ)
        Waypoint startNode = allWaypoints.Where(wp => wp != null).OrderBy(wp => Vector2.Distance(startPos, wp.transform.position)).FirstOrDefault();
        Waypoint endNode = allWaypoints.Where(wp => wp != null).OrderBy(wp => Vector2.Distance(targetPos, wp.transform.position)).FirstOrDefault();
        
        if (startNode == null || endNode == null) return path;

        // Стандартная реализация алгоритма Дейкстры/A*
        var distances = new Dictionary<Waypoint, float>();
        var previous = new Dictionary<Waypoint, Waypoint>();
        var unvisited = new List<Waypoint>(allWaypoints.Where(wp => wp != null));

        foreach (var wp in unvisited)
        {
            distances[wp] = float.MaxValue;
            previous[wp] = null;
        }
        distances[startNode] = 0;

        while (unvisited.Count > 0)
        {
            unvisited.Sort((a, b) => distances[a].CompareTo(distances[b]));
            Waypoint current = unvisited[0];
            unvisited.Remove(current);

            if (current == endNode)
            {
                // Путь найден, восстанавливаем его
                var pathList = new List<Waypoint>();
                for (Waypoint at = endNode; at != null; at = previous.ContainsKey(at) ? previous[at] : null)
                {
                    pathList.Add(at);
                }
                pathList.Reverse();
                path.Clear();
                foreach(var wp in pathList) { path.Enqueue(wp); }
                
                // Debug.Log($"[PathfindingUtility] Для {self.name} построен путь из {path.Count} точек.");
                return path;
            }

            if (current.neighbors == null) continue;

            foreach (var neighbor in current.neighbors)
            {
                if (neighbor == null || (neighbor.forbiddenTags != null && neighbor.forbiddenTags.Contains(self.tag))) continue;
                
                float alt = distances[current] + Vector2.Distance(current.transform.position, neighbor.transform.position);
                if (distances.ContainsKey(neighbor) && alt < distances[neighbor])
                {
                    distances[neighbor] = alt;
                    previous[neighbor] = current;
                }
            }
        }
        
        Debug.LogWarning($"[PathfindingUtility] Не удалось построить путь для {self.name} до {targetPos}");
        return path; // Возвращаем пустой путь, если цель недостижима
    }
}