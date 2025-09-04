using UnityEngine;

public class CameraToggle : MonoBehaviour
{
    [Header("Точки для переключения")]
    public Transform positionOne;
    public Transform positionTwo;

    [Header("Настройки")]
    public float moveSpeed = 10f;

    // --- Примечание: Все ссылки на UI и MusicPlayer временно убраны для максимальной простоты ---

    private bool isAtPositionOne = true;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = GetComponent<Camera>();
        // Устанавливаем начальную позицию
        if (positionOne != null)
        {
            Vector3 startPos = positionOne.position;
            startPos.z = transform.position.z;
            transform.position = startPos;
        }
    }

    // Публичный метод, который будет вызывать UI кнопка
    public void TogglePosition()
    {
        isAtPositionOne = !isAtPositionOne;
    }

    void LateUpdate()
    {
        // Переключение по Tab теперь просто вызывает наш публичный метод
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            TogglePosition();
        }

        // Логика движения
        if (mainCamera != null)
        {
            Vector3 targetMarkerPosition = isAtPositionOne ? positionOne.position : positionTwo.position;
            Vector3 targetPosition = new Vector3(targetMarkerPosition.x, targetMarkerPosition.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * moveSpeed);
        }
    }
}