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
        // --- ДИАГНОСТИКА НАЧАЛАСЬ ---
        Debug.Log("--- НАЧАТ ПРОЦЕСС ПЕРЕКЛЮЧЕНИЯ КАМЕРЫ ---");
        Debug.Log($"До нажатия, камера была в позиции 1: {isAtPositionOne}");

        isAtPositionOne = !isAtPositionOne;
        
        Debug.Log($"После нажатия, камера должна быть в позиции 1: {isAtPositionOne}");
        
        if (positionOne != null)
        {
            Debug.Log($"Координаты Position One: {positionOne.position}");
        }
        else
        {
            Debug.LogError("ССЫЛКА НА Position One ПУСТАЯ (NONE) В ИНСПЕКТОРЕ!");
        }

        if (positionTwo != null)
        {
            Debug.Log($"Координаты Position Two: {positionTwo.position}");
        }
        else
        {
            Debug.LogError("ССЫЛКА НА Position Two ПУСТАЯ (NONE) В ИНСПЕКТОРЕ!");
        }
        // --- ДИАГНОСТИКА ОКОНЧЕНА ---

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