using Managers;
using UnityEngine;
using UnityEngine.UI;


[RequireComponent(typeof(Button))]
public class ShowDirectorDeskButton : MonoBehaviour
{
    void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnShowDeskClicked);
    }


    private void OnShowDeskClicked()
    {
        if (MainUIManager.Instance != null)
        {
            MainUIManager.Instance.ShowDirectorDesk();
        }
    }
}
