using UnityEngine;
using UnityEngine.UI;

public class DirectorDocumentIcon : MonoBehaviour
{
    private ClientPathfinding ownerClient;
    private DirectorDocumentReviewPanel reviewPanel;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnIconClicked);
    }

    public void Setup(ClientPathfinding client, DirectorDocumentReviewPanel panel)
    {
        this.ownerClient = client;
        this.reviewPanel = panel;
    }

    private void OnIconClicked()
    {
        if (reviewPanel != null && ownerClient != null)
        {
            reviewPanel.ShowDocument(ownerClient);
        }
    }
}