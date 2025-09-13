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
    // ИЗМЕНЕНИЕ: Заменяем прямую ссылку на ссылку на посредника
    public CameraAudioLink audioLink; 

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
        
        // ИЗМЕНЕНИЕ: Ищем посредника, если он не назначен
        if (audioLink == null)
        {
            audioLink = GetComponent<CameraAudioLink>();
        }
        
        if (mainCamera != null && positionOne != null)
        {
            Vector3 startPos = positionOne.position;
            startPos.z = mainCamera.transform.position.z;
            mainCamera.transform.position = startPos;
        }
        
        UpdateUIVisibility();
        // ИЗМЕНЕНИЕ: Вызываем метод посредника
        audioLink?.ToggleMuffledAudio(!isAtPositionOne);
    }

    void LateUpdate()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput > 0f)
        {
            if (!isAtPositionOne)
            {
                SetPosition(true);
            }
        }
        else if (scrollInput < 0f)
        {
            if (isAtPositionOne)
            {
                SetPosition(false);
            }
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            TogglePosition();
        }
        
        if (mainCamera != null)
        {
            Vector3 targetMarkerPosition = isAtPositionOne ? positionOne.position : positionTwo.position;
            Vector3 targetPosition = new Vector3(targetMarkerPosition.x, targetMarkerPosition.y, mainCamera.transform.position.z);
            float deltaTime = Time.timeScale > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
			mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPosition, deltaTime * moveSpeed);
        }
    }
    
    public void TogglePosition()
    {
        SetPosition(!isAtPositionOne);
    }

    private void SetPosition(bool setToPositionOne)
    {
        isAtPositionOne = setToPositionOne;
        UpdateUIVisibility();
        // ИЗМЕНЕНИЕ: Вызываем метод посредника
        audioLink?.ToggleMuffledAudio(!isAtPositionOne);
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