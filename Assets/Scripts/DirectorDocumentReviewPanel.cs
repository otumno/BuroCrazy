using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class DirectorDocumentReviewPanel : MonoBehaviour
{
    private const string loremIpsumSource = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
    private const string ascenderLetters = "bdfhijklt";
    private const string descenderLetters = "gjpqy";

    [Header("UI Элементы")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI mandatesText;
    public TextMeshProUGUI feeAndBribeText;
    public Button approveButton;
    public Button rejectButton;
    public Button reviseButton;
    public Button approveWithBribeButton;
    public Button postponeButton;

    [Header("Настройки генерации текста")]
    [SerializeField] private int maxCapacity = 200;
    [SerializeField] private int wordCountToDisplay = 35;
    [SerializeField] private int maxStartIndex = 50;
    
    [Header("Префабы сетки документа")]
    public GameObject gridSquarePrefab;
    public GameObject gridSpacePrefab;
    public GameObject punctuationSquarePrefab;
    public Transform gridContainer;

    [Header("Отладка")]
    [SerializeField] private bool showDebugInfo = false;

    private ClientPathfinding currentClient;

    private void Start()
    {
        approveButton.onClick.AddListener(OnApproveClicked);
        approveWithBribeButton.onClick.AddListener(OnApproveWithBribeClicked);
        rejectButton.onClick.AddListener(OnRejectClicked);
        reviseButton.onClick.AddListener(OnReviseClicked);
        postponeButton.onClick.AddListener(OnPostponeClicked);
    }

    public void ShowDocument(ClientPathfinding client)
    {
        currentClient = client;
        Time.timeScale = 0f;
        gameObject.SetActive(true);
        titleText.text = DocumentTitleGenerator.GenerateTitle();
        feeAndBribeText.text = $"Пошлина: ${client.directorDocumentFee} | Взятка: ${client.directorDocumentBribe}";
        approveWithBribeButton.gameObject.SetActive(client.directorDocumentBribe > 0);
        GenerateDocumentGrid(client);
        UpdateMandatesText();
    }

    private void ClosePanel()
    {
        gameObject.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnApproveClicked()
    {
        if (currentClient == null) return;
        int totalSymbols = currentClient.directorDocumentLayout.gridState.Count(c => c == false);
        int errorCount = currentClient.directorDocumentLayout.gridState.Count(c => c == true);
        float actualErrorRate = (totalSymbols > 0) ? ((float)errorCount / totalSymbols) * 100f : 0f;
        float allowedErrorRate = DirectorManager.Instance.currentMandates.allowedDirectorErrorRate;

        if (actualErrorRate > allowedErrorRate)
        {
            Debug.LogWarning($"СТРАЙК! Директор одобрил документ с {actualErrorRate:F1}% ошибок при норме <= {allowedErrorRate}%.");
            DirectorManager.Instance.AddStrike();
        }

        currentClient.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
        currentClient.stateMachine.SetState(ClientState.MovingToGoal);
        StartOfDayPanel.Instance.RemoveDocumentIcon(currentClient);
        ClosePanel();
        currentClient = null;
    }

    public void OnApproveWithBribeClicked()
    {
        if (currentClient == null) return;
        if (PlayerWallet.Instance != null) { PlayerWallet.Instance.AddMoney(currentClient.directorDocumentBribe, transform.position); }
        OnApproveClicked();
    }

    public void OnRejectClicked()
    {
        if (currentClient == null) return;
        currentClient.reasonForLeaving = ClientPathfinding.LeaveReason.Upset;
        currentClient.stateMachine.SetGoal(ClientSpawner.Instance.exitWaypoint);
        currentClient.stateMachine.SetState(ClientState.LeavingUpset);
        StartOfDayPanel.Instance.RemoveDocumentIcon(currentClient);
        ClosePanel();
        currentClient = null;
    }

    public void OnReviseClicked()
    {
        if (currentClient == null) return;
        currentClient.hasBeenSentForRevision = true;
        currentClient.stateMachine.SetGoal(ClientSpawner.Instance.formTable.tableWaypoint);
        currentClient.stateMachine.SetState(ClientState.MovingToGoal);
        StartOfDayPanel.Instance.RemoveDocumentIcon(currentClient);
        ClosePanel();
        currentClient = null;
    }

    public void OnPostponeClicked() { ClosePanel(); }
    
    private void GenerateDocumentGrid(ClientPathfinding client)
    {
        foreach (Transform child in gridContainer) { Destroy(child.gameObject); }
        
        if (client.directorDocumentLayout == null || client.directorDocumentLayout.gridState.Count != maxCapacity)
        {
            client.directorDocumentLayout = new DirectorDocumentLayout();
            float errorPercentage = (1.0f - client.documentQuality) * 100f;
            int errorCount = Mathf.FloorToInt(maxCapacity * (errorPercentage / 100f));
            for(int i = 0; i < maxCapacity; i++) { client.directorDocumentLayout.gridState.Add(false); }
            for (int i = 0; i < errorCount; i++)
            {
                int randomIndex = Random.Range(0, maxCapacity);
                if (!client.directorDocumentLayout.gridState[randomIndex]) { client.directorDocumentLayout.gridState[randomIndex] = true; } 
                else { i--; }
            }
        }
        else if(client.hasBeenSentForRevision)
        {
            for(int i = 0; i < client.directorDocumentLayout.gridState.Count; i++)
            {
                if(client.directorDocumentLayout.gridState[i] == true)
                {
                    float chanceToFix = 1.0f - client.suetunFactor; 
                    if(Random.value < chanceToFix) { client.directorDocumentLayout.gridState[i] = false; }
                }
            }
            client.hasBeenSentForRevision = false;
        }

        int gridIndex = 0;
        int wordsProcessed = 0;
        bool isFirstLetter = true;
        bool capitalizeNextLetter = false;

        int randomStart = Random.Range(0, maxStartIndex);
        int actualStartIndex = loremIpsumSource.IndexOf(' ', randomStart) + 1;
        if (actualStartIndex <= 0) actualStartIndex = 0;
        
        List<string> wordList = new List<string>();
        string[] allWords = loremIpsumSource.Split(' ');
        for (int i = actualStartIndex; i < allWords.Length && wordList.Count < wordCountToDisplay; i++)
        {
            wordList.Add(allWords[i]);
        }
        string textToShow = string.Join(" ", wordList);
        if (textToShow.Length > 0 && !char.IsPunctuation(textToShow[textToShow.Length - 1]))
        {
            textToShow += ".";
        }
        
        foreach(char c in textToShow)
        {
            if (gridIndex >= maxCapacity) break;
            
            GameObject square;
            RectTransform rect;

            if (c == ' ')
            {
                Instantiate(gridSpacePrefab, gridContainer);
                wordsProcessed++;
            }
            else if (char.IsPunctuation(c))
            {
                square = Instantiate(punctuationSquarePrefab, gridContainer);
                capitalizeNextLetter = true;
            }
            else
            {
                square = Instantiate(gridSquarePrefab, gridContainer);
                rect = square.GetComponent<RectTransform>();

                if(isFirstLetter || capitalizeNextLetter)
                {
                    rect.localScale = new Vector3(1, 1.2f, 1);
                    isFirstLetter = false;
                    capitalizeNextLetter = false;
                }
                else if (ascenderLetters.Contains(c))
                {
                    rect.localScale = new Vector3(1, 1.13f, 1);
                    rect.anchoredPosition += new Vector2(0, rect.sizeDelta.y * 0.065f);
                }
                else if (descenderLetters.Contains(c))
                {
                    rect.localScale = new Vector3(1, 1.13f, 1);
                    rect.anchoredPosition -= new Vector2(0, rect.sizeDelta.y * 0.065f);
                }
            }
            
            if (gridIndex < client.directorDocumentLayout.gridState.Count && client.directorDocumentLayout.gridState[gridIndex])
            {
                GameObject createdObject = gridContainer.GetChild(gridContainer.childCount - 1).gameObject;
                if (createdObject != null && createdObject.TryGetComponent<Image>(out var image)) { image.color = Color.red; }
            }
            gridIndex++;
        }
    }

    private void UpdateMandatesText()
    {
        if (DirectorManager.Instance == null || DirectorManager.Instance.currentMandates == null) return;
        
        float allowedErrorRate = DirectorManager.Instance.currentMandates.allowedDirectorErrorRate;

        if (showDebugInfo && currentClient != null && currentClient.directorDocumentLayout != null)
        {
            int errorCount = currentClient.directorDocumentLayout.gridState.Count(c => c == true);
            mandatesText.text = $"НОРМА: <= {allowedErrorRate}%\n<color=red>ОТЛАДКА: В документе {errorCount} ошибок.</color>";
        }
        else 
        { 
            mandatesText.text = $"Норма дня: Ошибок не более {allowedErrorRate}%"; 
        }
    }
}