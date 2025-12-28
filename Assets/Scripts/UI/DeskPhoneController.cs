using UnityEngine;
using Managers;

public class DeskPhoneController : DeskInteractiveItem
{
    private void Update()
    {
        // В Update проверяем, есть ли звонки, чтобы мигать лампочкой
        if (PhoneManager.Instance != null)
        {
            bool isRinging = PhoneManager.Instance.HasActiveCalls;
            SetNotificationState(isRinging);
        }
    }
    
    // Этот метод привяжем в OnClick в инспекторе
    public void OpenPhoneInterface()
    {
        if (PhoneManager.Instance != null && PhoneManager.Instance.phonePanelUI != null)
        {
            PhoneManager.Instance.phonePanelUI.Show();
        }
    }
}