// Файл: DirectorDeskButton.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DirectorDeskButton : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    public TextMeshProUGUI countText;
    public GameObject documentIconPrefab;
    public Transform iconContainer;
    
    [Header("Настройки стопки")]
    public int maxVisibleIcons = 10;
    public float yOffsetPerIcon = 5f;
    public float maxRandomXOffset = 3f;
    public float maxRandomRotation = 5f;
    
    [Header("Звук")]
    public AudioClip newDocumentSound;
    
    private AudioSource uiAudioSource;
    private List<GameObject> activeIcons = new List<GameObject>();
    private Button thisButton;

    void Start()
    {
        thisButton = GetComponent<Button>();
        thisButton.onClick.AddListener(OnClick);
        
        // Получаем AudioSource от нового менеджера
        if (MainUIManager.Instance != null)
        {
            uiAudioSource = MainUIManager.Instance.uiAudioSource;
        }

        if (StartOfDayPanel.Instance != null)
        {
            UpdateAppearance(StartOfDayPanel.Instance.GetWaitingDocumentCount());
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void UpdateAppearance(int count)
    {
        gameObject.SetActive(count > 0);
        if (count > 0)
        {
            if (count > activeIcons.Count && newDocumentSound != null && uiAudioSource != null)
            {
                uiAudioSource.PlayOneShot(newDocumentSound);
            }
            countText.text = $"На рассмотрении: {count}";
        }

        foreach (GameObject icon in activeIcons)
        {
            Destroy(icon);
        }
        activeIcons.Clear();

        int iconsToCreate = Mathf.Min(count, maxVisibleIcons);
        for (int i = 0; i < iconsToCreate; i++)
        {
            GameObject newIcon = Instantiate(documentIconPrefab, iconContainer);
            RectTransform rt = newIcon.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(
                    Random.Range(-maxRandomXOffset, maxRandomXOffset), 
                    i * yOffsetPerIcon
                );
                rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-maxRandomRotation, maxRandomRotation));
            }
            activeIcons.Add(newIcon);
        }
    }

    public void OnClick()
    {
        // Обращаемся к правильному менеджеру
        if (MainUIManager.Instance != null)
        {
            MainUIManager.Instance.ShowPausePanel(true);
        }
    }
}