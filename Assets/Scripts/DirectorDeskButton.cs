// Файл: DirectorDeskButton.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DirectorDeskButton : MonoBehaviour
{
    [Header("Ссылки на компоненты")]
    [Tooltip("Текстовое поле для счетчика документов")]
    public TextMeshProUGUI countText;
    [Tooltip("Префаб ОДНОЙ иконки документа, которая будет копироваться")]
    public GameObject documentIconPrefab;
    [Tooltip("Объект-контейнер, внутри которого будут создаваться иконки")]
    public Transform iconContainer;
    
    [Header("Настройки стопки")]
    [Tooltip("Максимальное количество видимых иконок в стопке")]
    public int maxVisibleIcons = 10;
    [Tooltip("Небольшое смещение для каждой новой иконки по оси Y")]
    public float yOffsetPerIcon = 5f;
    [Tooltip("Максимальное случайное смещение по оси X для создания небрежности")]
    public float maxRandomXOffset = 3f;
    [Tooltip("Максимальный случайный угол поворота для создания небрежности")]
    public float maxRandomRotation = 5f;
    
    [Header("Звук")]
    [Tooltip("Звук, который проигрывается при добавлении нового документа в стопку")]
    public AudioClip newDocumentSound;
    
    private AudioSource uiAudioSource;
    private List<GameObject> activeIcons = new List<GameObject>();
    private Button thisButton;

    void Start()
    {
        thisButton = GetComponent<Button>();
        if (thisButton != null)
        {
            thisButton.onClick.AddListener(OnClick);
        }

        if (MenuManager.Instance != null)
        {
            uiAudioSource = MenuManager.Instance.uiAudioSource;
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
    if (MenuManager.Instance != null)
    {
        MenuManager.Instance.ShowPausePanel();
    }
}
}