using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StaffNameLabelUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI roleText;
    [SerializeField] private Image background;
    
    // --- НОВОЕ: Кнопка самого имени ---
    [SerializeField] private Button nameButton; 

    private StaffController _staff;
    private System.Action<StaffController> _onClickAction;

    // Добавляем аргумент onClickAction
    public void Setup(StaffController staff, RoleColorDatabase colorDb, System.Action<StaffController> onClickAction)
    {
        _staff = staff;
        _onClickAction = onClickAction;

        if (nameText != null) nameText.text = staff.characterName;
        if (roleText != null) roleText.text = staff.currentRole.ToString();

        if (background != null && colorDb != null)
        {
            background.color = colorDb.GetColorForRole(staff.currentRole, Color.gray);
        }

        if (nameButton != null)
        {
            nameButton.onClick.RemoveAllListeners();
            nameButton.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        // Вызываем переданный метод
        _onClickAction?.Invoke(_staff);
    }
}