// Файл: CameraToggle.cs
using UnityEngine;
using System.Collections.Generic;

public class CameraToggle : MonoBehaviour
{
    [Header("Камера и точки")]
    public Camera mainCamera;
    public Transform positionOne;
    public Transform positionTwo;
    
    // --- НОВОЕ: Ссылка на плеер для управления звуком ---
    [Header("Связанные системы")]
    [Tooltip("Перетащите сюда объект с MusicPlayer")]
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
        
        // --- НОВОЕ: Устанавливаем начальное состояние звука ---
        musicPlayer?.SetMuffled(!isAtPositionOne);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            TogglePosition();
        }

        if (mainCamera != null)
        {
            Vector3 targetMarkerPosition = isAtPositionOne ? positionOne.position : positionTwo.position;
            Vector3 targetPosition = new Vector3(targetMarkerPosition.x, targetMarkerPosition.y, mainCamera.transform.position.z);
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPosition, Time.deltaTime * moveSpeed);
        }
    }

    public void TogglePosition()
    {
        isAtPositionOne = !isAtPositionOne;
        UpdateUIVisibility();
        
        // --- НОВОЕ: Вызываем метод для изменения звука ---
        // Если мы не у первой позиции (т.е. у второй, "нижней"), то звук нужно приглушить (isMuffled = true)
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