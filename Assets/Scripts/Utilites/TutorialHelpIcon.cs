// Файл: Assets/Scripts/UI/Tutorial/TutorialHelpIcon.cs
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TutorialHelpIcon : MonoBehaviour
{
    [Tooltip("Если true, при каждом нажатии туториал будет начинаться с начала")]
    [SerializeField] private bool resetOnOpen = true;

    private Button helpButton;

    void Awake()
    {
        helpButton = GetComponent<Button>();
        helpButton.onClick.AddListener(OnHelpIconClicked);
    }

    private void OnHelpIconClicked()
    {
        if (TutorialMascot.Instance == null)
        {
            Debug.LogError("[TutorialHelpIcon] Не могу найти TutorialMascot.Instance!");
            return;
        }

        if (resetOnOpen)
        {
            // Сбрасываем прогресс для текущего экрана
            TutorialMascot.Instance.ResetCurrentScreenTutorial();
        }
        else
        {
            // Просто показываем (или прячем, если уже показан)
            TutorialMascot.Instance.ToggleHelp();
        }
    }
}