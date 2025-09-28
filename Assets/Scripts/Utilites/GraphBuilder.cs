using UnityEngine;
using System.Collections.Generic;

public class GraphBuilder : MonoBehaviour
{
    public LayerMask obstacleMask;
    public float maxDistance = 100f;
    [Tooltip("Радиус 'толщины' пути. Чем больше, тем дальше от стен будут строиться маршруты.")]
    public float pathRadius = 0.3f;

    [ContextMenu("Build Graph Now")]
    public void BuildGraph()
    {
        Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        if (allWaypoints.Length == 0) return;

        foreach (var wp1 in allWaypoints)
        {
            if (wp1 == null) continue;
            wp1.neighbors = new List<Waypoint>();
            foreach (var wp2 in allWaypoints)
            {
                if (wp1 == wp2 || wp2 == null) continue;

                Vector2 direction = (wp2.transform.position - wp1.transform.position).normalized;
                float distance = Vector2.Distance(wp1.transform.position, wp2.transform.position);
				
				if (distance > maxDistance) continue;
				
                RaycastHit2D hit = Physics2D.CircleCast(wp1.transform.position, pathRadius, direction, distance, obstacleMask);
                
                if (hit.collider == null)
                {
                    wp1.neighbors.Add(wp2);
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        Waypoint[] allWaypoints = FindObjectsByType<Waypoint>(FindObjectsSortMode.None);
        foreach (var wp in allWaypoints)
        {
            if (wp == null || wp.neighbors == null) continue;

            Gizmos.color = Color.green;
            foreach (var neighbor in wp.neighbors)
            {
                if (neighbor != null)
                {
                    Gizmos.DrawLine(wp.transform.position, neighbor.transform.position);
                }
            }
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(wp.transform.position, 0.1f);
        }
    }
}