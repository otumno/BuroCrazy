using UnityEngine;

public class RectZone : MonoBehaviour
{
    public Vector2 center;
    public float width;
    public float height;

    void Start()
    {
        if (center == Vector2.zero)
        {
            center = new Vector2(transform.position.x, transform.position.y);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green; // Цвет границы зоны
        Vector3 topLeft = new Vector3(center.x - width / 2, center.y + height / 2, 0);
        Vector3 bottomRight = new Vector3(center.x + width / 2, center.y - height / 2, 0);
        Gizmos.DrawWireCube(new Vector3(center.x, center.y, 0), new Vector3(width, height, 0));
    }
}