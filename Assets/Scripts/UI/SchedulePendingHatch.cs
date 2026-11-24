using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>Hatches every assigned period, including a newly hired employee before their first shift.</summary>
    public class SchedulePendingHatch : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            const float spacing = 10f;
            const float thickness = 1.5f;
            for (float x = -rect.height; x < rect.width; x += spacing)
            {
                float low = Mathf.Max(0, -x);
                float high = Mathf.Min(rect.height, rect.width - x);
                if (high <= low) continue;
                var start = new Vector2(rect.xMin + x + low, rect.yMin + low);
                var end = new Vector2(rect.xMin + x + high, rect.yMin + high);
                int index = mesh.currentVertCount;
                mesh.AddVert(start, color, Vector2.zero);
                mesh.AddVert(new Vector2(Mathf.Min(rect.xMax, start.x + thickness), start.y), color, Vector2.zero);
                mesh.AddVert(new Vector2(Mathf.Min(rect.xMax, end.x + thickness), end.y), color, Vector2.zero);
                mesh.AddVert(end, color, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2);
                mesh.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
