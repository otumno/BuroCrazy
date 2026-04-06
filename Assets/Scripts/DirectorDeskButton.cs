// Файл: DirectorDeskButton.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Managers;

public class DirectorDeskButton : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    public TextMeshProUGUI countText;
    public GameObject documentIconPrefab;
    public Transform iconContainer;
    
    [Header("Client Icons")]
    [SerializeField] private Sprite audienceSprite;  // силуэт для DirectorAudience
    [SerializeField] private Sprite documentSprite;  // иконка документа (существующее поведение)
    
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
            if (countText != null)
            {
                countText.text = $"Ожидают: {count}";
            }
        }

        foreach (GameObject icon in activeIcons)
        {
            Destroy(icon);
        }
        activeIcons.Clear();

        // Получаем список клиентов в приемной, чтобы знать, кто ждет
        var waitingClients = ClientSpawner.Instance?.directorReceptionZone?.GetOccupyingClients() ?? new List<ClientPathfinding>();

        int iconsToCreate = Mathf.Min(count, maxVisibleIcons);
        for (int i = 0; i < iconsToCreate; i++)
        {
            GameObject newIcon = Instantiate(documentIconPrefab, iconContainer);
            
            // Находим Image у клонированной иконки
            Image iconImage = newIcon.GetComponent<Image>();
            if (iconImage == null) iconImage = newIcon.GetComponentInChildren<Image>();

            if (iconImage != null)
            {
                // Проверяем цель клиента по его индексу в очереди
                bool isAudience = false;
                if (i < waitingClients.Count && waitingClients[i] != null && waitingClients[i].mainGoal == ClientGoal.DirectorAudience)
                {
                    isAudience = true;
                }

                // Назначаем нужный спрайт прямо на созданный элемент стопки
                if (isAudience && audienceSprite != null)
                {
                    iconImage.sprite = audienceSprite;
                }
                else if (documentSprite != null)
                {
                    iconImage.sprite = documentSprite;
                }
            }

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