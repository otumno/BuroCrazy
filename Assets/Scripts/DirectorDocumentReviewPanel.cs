using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class DirectorDocumentReviewPanel : MonoBehaviour
{
    // ... (весь ваш код до метода OnApproveClicked остается без изменений) ...

    [System.Serializable]
    public class CharacterStyle
    {
        [Tooltip("Желаемый размер буквы (ширина, высота). Например, (10, 15) для высокой буквы.")]
        public Vector2 preferredSize = new Vector2(10, 12);
    }

    private const string vowelsLower = "аеёиоуыэюя";
    private const string vowelsUpper = "АЕЁИОУЫЭЮЯ";
    private const string consonantsLower = "бвгджзйклмнпрстфхцчшщ";
    private const string consonantsUpper = "БВГДЖЗЙКЛМНПРСТФХЦЧШЩ";

    [Header("UI Элементы")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI mandatesText;
    public TextMeshProUGUI feeAndBribeText;
    public TextMeshProUGUI documentText;
    public Button approveButton;
    public Button rejectButton;
    public Button reviseButton;
    public Button approveWithBribeButton;
    public Button postponeButton;

    [Header("Настройки стиля 'букв'")]
    public CharacterStyle capitalLetterStyle;
    public CharacterStyle ascenderLetterStyle;
    public CharacterStyle descenderLetterStyle;
    
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
    
    private void GenerateDocumentGrid(ClientPathfinding client)
    {
        string cleanText = BureaucraticTextGenerator.GenerateParagraph();

        if (client.directorDocumentLayout == null || client.directorDocumentLayout.gridState.Count != cleanText.Length)
        {
            client.directorDocumentLayout = new DirectorDocumentLayout();
            var gridState = new bool[cleanText.Length];
            
            List<int> validErrorIndices = new List<int>();
            bool isInsideTag = false;
            for (int i = 0; i < cleanText.Length; i++)
            {
                if (cleanText[i] == '<') isInsideTag = true;
                
                if (char.IsLetter(cleanText[i]) && !isInsideTag)
                {
                    validErrorIndices.Add(i);
                }
                
                if (cleanText[i] == '>') isInsideTag = false;
            }

            float errorPercentage = (1.0f - client.documentQuality) / 2f;
            int errorCount = Mathf.FloorToInt(validErrorIndices.Count * errorPercentage);
            
            for (int i = 0; i < errorCount; i++)
            {
                if (validErrorIndices.Count == 0) break;

                int randomListIndex = Random.Range(0, validErrorIndices.Count);
                int actualTextIndex = validErrorIndices[randomListIndex];
                
                gridState[actualTextIndex] = true;
                
                validErrorIndices.RemoveAt(randomListIndex);
            }
            client.directorDocumentLayout.gridState = new List<bool>(gridState);
        }
        
        StringBuilder textWithTypos = new StringBuilder(cleanText);
        for (int i = 0; i < cleanText.Length; i++)
        {
            if (client.directorDocumentLayout.gridState[i])
            {
                textWithTypos[i] = GetRandomTypo(cleanText[i]);
            }
        }

        StringBuilder richTextBuilder = new StringBuilder();
        for (int i = 0; i < textWithTypos.Length; i++)
        {
            if (client.directorDocumentLayout.gridState[i] == true)
            {
                richTextBuilder.Append($"<color=red>{textWithTypos[i]}</color>");
            }
            else
            {
                richTextBuilder.Append(textWithTypos[i]);
            }
        }
        
        documentText.text = richTextBuilder.ToString();
    }

    private char GetRandomTypo(char originalChar)
    {
        if (vowelsLower.Contains(originalChar)) return vowelsLower[Random.Range(0, vowelsLower.Length)];
        if (vowelsUpper.Contains(originalChar)) return vowelsUpper[Random.Range(0, vowelsUpper.Length)];
        if (consonantsLower.Contains(originalChar)) return consonantsLower[Random.Range(0, consonantsLower.Length)];
        if (consonantsUpper.Contains(originalChar)) return consonantsUpper[Random.Range(0, consonantsUpper.Length)];
        return originalChar;
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
        
        if(MainUIManager.Instance != null && !MainUIManager.Instance.isTransitioning)
        {
            Time.timeScale = 1f;
        }
    }

    public void OnApproveClicked()
    {
        if (currentClient == null) return;

        int errorCount = currentClient.directorDocumentLayout.gridState.Count(c => c == true);
        float textLength = currentClient.directorDocumentLayout.gridState.Count > 0 ? currentClient.directorDocumentLayout.gridState.Count : 1;
        
        // <<< НАЧАЛО ИСПРАВЛЕНИЙ №1 >>>
        // Считаем реальный процент ошибок (от 0.0 до 1.0)
        float actualErrorRate = errorCount / textLength;
        
        float allowedErrorRate = 1f; // Значение по умолчанию (100%), если поручений нет
        // Проверяем, есть ли вообще поручения на сегодня
        if (DirectorManager.Instance.currentMandates.Count > 0)
        {
            // Берем ПЕРВЫЙ приказ из списка
            DirectorOrder currentMandate = DirectorManager.Instance.currentMandates[0];
            // Теперь получаем ЕГО личный допустимый уровень ошибок
            allowedErrorRate = currentMandate.allowedDirectorErrorRate;
        }
        // <<< КОНЕЦ ИСПРАВЛЕНИЙ №1 >>>

        if (actualErrorRate > allowedErrorRate)
        {
            Debug.LogWarning($"СТРАЙК! Директор одобрил документ с {errorCount} ошибками ({actualErrorRate:P1}) при норме <= {allowedErrorRate:P1}.");
            DirectorManager.Instance.AddStrike();
        }
		
		DocumentQualityManager.Instance?.RegisterProcessedDocument(currentClient.documentQuality); // ПРАВИЛЬНО
		currentClient.billToPay = currentClient.directorDocumentFee;

        currentClient.billToPay = currentClient.directorDocumentFee;
        currentClient.stateMachine.SetGoal(ClientSpawner.GetCashierZone().waitingWaypoint);
        currentClient.stateMachine.SetState(ClientState.MovingToGoal);
        StartOfDayPanel.Instance.RemoveDocumentIcon(currentClient);
        ClosePanel();
        currentClient = null;
    }

    public void OnApproveWithBribeClicked()
    {
        if (currentClient == null) return;
        if (PlayerWallet.Instance != null) 
{
    PlayerWallet.Instance.AddMoney(currentClient.directorDocumentBribe, "Взятка за визу Директора", IncomeType.Shadow);
}
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

    // --- ИЗМЕНЕНИЕ НАЧАЛО ---
    currentClient.stateMachine.GoGetFormAndReturn();
    // --- ИЗМЕНЕНИЕ КОНЕЦ ---
    
    StartOfDayPanel.Instance.RemoveDocumentIcon(currentClient);
    ClosePanel();
    currentClient = null;
}

    public void OnPostponeClicked() 
    {
        ClosePanel();
    }

    private void UpdateMandatesText()
    {
        if (DirectorManager.Instance == null || DirectorManager.Instance.currentMandates == null) return;

        // <<< НАЧАЛО ИСПРАВЛЕНИЙ №2 >>>
        float allowedErrorRate = 1f; // Значение по умолчанию (100%)
        if (DirectorManager.Instance.currentMandates.Count > 0)
        {
            // Берем ПЕРВЫЙ приказ из списка и его свойство
            allowedErrorRate = DirectorManager.Instance.currentMandates[0].allowedDirectorErrorRate;
        }
        // <<< КОНЕЦ ИСПРАВЛЕНИЙ №2 >>>

        if (showDebugInfo && currentClient != null && currentClient.directorDocumentLayout != null)
        {
            int errorCount = currentClient.directorDocumentLayout.gridState.Count(c => c == true);
            int totalChars = currentClient.directorDocumentLayout.gridState.Count;
            float actualErrorRate = (totalChars > 0) ? ((float)errorCount / totalChars) : 0f;
            
            // Используем форматирование P0 для процентов без знаков после запятой
            mandatesText.text = $"НОРМА: <= {allowedErrorRate:P0}\n<color=red>ОТЛАДКА: {errorCount} ошибок ({actualErrorRate:P1})</color>";
        }
        else 
        { 
            // Используем P0 для красивого отображения процентов (например, "10%" вместо "0.1%")
            mandatesText.text = $"Норма дня: Ошибок не более {allowedErrorRate:P0}";
        }
    }
}