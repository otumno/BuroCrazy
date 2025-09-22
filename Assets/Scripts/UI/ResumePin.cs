// Файл: ResumePin.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class ResumePin : MonoBehaviour
{
    [Header("UI Компоненты 'Листка'")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject uniqueSkillIcon;
    [SerializeField] private Button viewButton;

    private Candidate candidate;
    // --- ИЗМЕНЕНИЕ ЗДЕСЬ: Указываем новый, правильный тип контроллера ---
    private HiringSystemUI panelController; 

    // --- И ИЗМЕНЕНИЕ ЗДЕСЬ: Принимаем в метод новый тип ---
    public void Setup(Candidate cand, HiringSystemUI controller)
    {
        this.candidate = cand;
        this.panelController = controller;

        nameText.text = candidate.Name;
        
        if (uniqueSkillIcon != null)
        {
            uniqueSkillIcon.SetActive(candidate.UniqueActionsPool.Any());
        }

        viewButton.onClick.RemoveAllListeners();
        viewButton.onClick.AddListener(OnView);
    }

    private void OnView()
    {
        // Теперь этот вызов будет работать, так как panelController правильного типа
        panelController.ShowDetailedView(this.candidate, this.gameObject);
    }
}