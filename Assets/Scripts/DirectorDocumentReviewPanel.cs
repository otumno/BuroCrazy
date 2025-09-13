// Файл: DirectorDocumentReviewPanel.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class DirectorDocumentReviewPanel : MonoBehaviour
{
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
        // 1. Генерируем чистый текст с тегами
        string cleanText = BureaucraticTextGenerator.GenerateParagraph();

        // 2. Создаем или обновляем карту ошибок, ИГНОРИРУЯ ТЕГИ
        if (client.directorDocumentLayout == null || client.directorDocumentLayout.gridState.Count != cleanText.Length)
        {
            client.directorDocumentLayout = new DirectorDocumentLayout();
            var gridState = new bool[cleanText.Length];
            
            // --- НОВАЯ УМНАЯ ЛОГИКА РАЗМЕЩЕНИЯ ОШИБОК ---
            List<int> validErrorIndices = new List<int>();
            bool isInsideTag = false;
            for (int i = 0; i < cleanText.Length; i++)
            {
                if (cleanText[i] == '<') isInsideTag = true;
                
                // Добавляем индекс в список кандидатов на ошибку, только если это буква и она не внутри тега
                if (char.IsLetter(cleanText[i]) && !isInsideTag)
                {
                    validErrorIndices.Add(i);
                }
                
                if (cleanText[i] == '>') isInsideTag = false;
            }

            float errorPercentage = (1.0f - client.documentQuality) / 2f;
            int errorCount = Mathf.FloorToInt(validErrorIndices.Count * errorPercentage);
            
            // Ставим ошибки только в разрешенных местах
            for (int i = 0; i < errorCount; i++)
            {
                if (validErrorIndices.Count == 0) break; // На случай, если букв меньше, чем желаемых ошибок

                int randomListIndex = Random.Range(0, validErrorIndices.Count);
                int actualTextIndex = validErrorIndices[randomListIndex];
                
                gridState[actualTextIndex] = true;
                
                validErrorIndices.RemoveAt(randomListIndex); // Удаляем, чтобы не поставить две ошибки в одно место
            }
            client.directorDocumentLayout.gridState = new List<bool>(gridState);
            // --- КОНЕЦ НОВОЙ ЛОГИКИ ---
        }
        
        // 3. Создаем опечатки в тексте на основе "умной" карты ошибок
        StringBuilder textWithTypos = new StringBuilder(cleanText);
        for (int i = 0; i < cleanText.Length; i++)
        {
            if (client.directorDocumentLayout.gridState[i])
            {
                textWithTypos[i] = GetRandomTypo(cleanText[i]);
            }
        }

        // 4. Собираем финальный rich text, подсвечивая места, где были сделаны опечатки
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
        
        // 5. Присваиваем готовый текст
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
        if(MenuManager.Instance != null && !MenuManager.Instance.isTransitioning)
        {
            Time.timeScale = 1f;
        }
    }

    public void OnApproveClicked()
    {
        if (currentClient == null) return;
        int errorCount = currentClient.directorDocumentLayout.gridState.Count(c => c == true);
        float textLength = currentClient.directorDocumentLayout.gridState.Count > 0 ? currentClient.directorDocumentLayout.gridState.Count : 1;
        float actualErrorRate = (errorCount / textLength) * 100f;
        float allowedErrorRate = DirectorManager.Instance.currentMandates.allowedDirectorErrorRate;

        if (actualErrorRate > allowedErrorRate)
        {
            Debug.LogWarning($"СТРАЙК! Директор одобрил документ с {errorCount} ошибками ({actualErrorRate:F1}%) при норме <= {allowedErrorRate}%.");
            DirectorManager.Instance.AddStrike();
        }
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

    public void OnPostponeClicked() 
    {
        ClosePanel();
    }

    private void UpdateMandatesText()
    {
        if (DirectorManager.Instance == null || DirectorManager.Instance.currentMandates == null) return;
        float allowedErrorRate = DirectorManager.Instance.currentMandates.allowedDirectorErrorRate;
        if (showDebugInfo && currentClient != null && currentClient.directorDocumentLayout != null)
        {
            int errorCount = currentClient.directorDocumentLayout.gridState.Count(c => c == true);
            int totalChars = currentClient.directorDocumentLayout.gridState.Count;
            float actualErrorRate = (totalChars > 0) ? ((float)errorCount / totalChars) * 100f : 0f;
            
            mandatesText.text = $"НОРМА: <= {allowedErrorRate}%\n<color=red>ОТЛАДКА: {errorCount} ошибок ({actualErrorRate:F1}%)</color>";
        }
        else 
        { 
            mandatesText.text = $"Норма дня: Ошибок не более {allowedErrorRate}%";
        }
    }
}