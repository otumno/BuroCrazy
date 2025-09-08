// Файл: CameraToggle.cs
using UnityEngine;
using System.Collections.Generic;

public class CameraToggle : MonoBehaviour
{
    [Header("Камера и точки")]
    public Camera mainCamera;
    public Transform positionOne;
    public Transform positionTwo;
    
    [Header("Связанные системы")]
    public MusicPlayer musicPlayer;
    
    [Header("Настройки")]
    public float moveSpeed = 10f;
    
    [Header("UI для переключения (Черный список)")]
    public List<GameObject> allToggleableUI;
    public List<GameObject> hideInPositionOne;
    public List<GameObject> hideInPositionTwo;

    private bool isAtPositionOne = true;
    
    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        
        if (mainCamera != null && positionOne != null)
        {
            Vector3 startPos = positionOne.position;
            startPos.z = mainCamera.transform.position.z;
            mainCamera.transform.position = startPos;
        }
        
        UpdateUIVisibility();
        musicPlayer?.SetMuffled(!isAtPositionOne);
    }

    void LateUpdate()
    {
        // --- Логика для колесика мыши (остается без изменений) ---
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollInput > 0f) // Скролл вверх
        {
            if (!isAtPositionOne)
            {
                SetPosition(true);
            }
        }
        else if (scrollInput < 0f) // Скролл вниз
        {
            if (isAtPositionOne)
            {
                SetPosition(false);
            }
        }

        // --- НОВАЯ ЛОГИКА: Возвращаем управление клавишей Tab ---
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            TogglePosition();
        }
        // ---------------------------------------------------------

        // Логика плавного движения камеры (остается без изменений)
        if (mainCamera != null)
        {
            Vector3 targetMarkerPosition = isAtPositionOne ? positionOne.position : positionTwo.position;
            Vector3 targetPosition = new Vector3(targetMarkerPosition.x, targetMarkerPosition.y, mainCamera.transform.position.z);
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPosition, Time.deltaTime * moveSpeed);
        }
    }

    /// <summary>
    /// Этот публичный метод будет вызываться по нажатию на Tab или на кнопку в UI.
    /// </summary>
    public void TogglePosition()
    {
        // Просто переключаем на противоположную позицию
        SetPosition(!isAtPositionOne);
    }

    // Этот приватный метод устанавливает конкретную позицию и обновляет все системы
    private void SetPosition(bool setToPositionOne)
    {
        isAtPositionOne = setToPositionOne;
        UpdateUIVisibility();
        musicPlayer?.SetMuffled(!isAtPositionOne);
    }

    void UpdateUIVisibility()
    {
        List<GameObject> activeBlacklist = isAtPositionOne ? hideInPositionOne : hideInPositionTwo;
        foreach (var uiObject in allToggleableUI)
        {
            if (uiObject != null)
            {
                uiObject.SetActive(!activeBlacklist.Contains(uiObject));
            }
        }
    }
}