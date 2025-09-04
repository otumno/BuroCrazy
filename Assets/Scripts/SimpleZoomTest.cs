using UnityEngine;

public class SimpleZoomTest : MonoBehaviour
{
    [Tooltip("Перетащите сюда вашу Main Camera")]
    public Camera theCamera;

    public float defaultSize = 5.4f;
    public float zoomedSize = 2.0f;

    void LateUpdate()
    {
        if (theCamera == null)
        {
            return;
        }

        // Если зажата клавиша Z, ставим приближенный размер
        if (Input.GetKey(KeyCode.Z))
        {
            theCamera.orthographicSize = zoomedSize;
        }
        // Если не зажата, возвращаем стандартный размер
        else
        {
            theCamera.orthographicSize = defaultSize;
        }
    }
}