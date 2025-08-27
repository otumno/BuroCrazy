using UnityEngine;
using TMPro;

public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; private set; }

    [Header("UI Компоненты")]
    [Tooltip("Текстовое поле для отображения количества денег")]
    public TextMeshProUGUI moneyText;
    [Tooltip("Префаб эффекта 'летящих денег', который появляется при оплате")]
    public GameObject moneyEffectPrefab;

    private int currentMoney = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }
    }

    void Start()
    {
        UpdateMoneyText();
    }

    public void AddMoney(int amount, Vector3 spawnPosition)
    {
        currentMoney += amount;
        UpdateMoneyText();

        /* // ЭФФЕКТ ОТКЛЮЧЕН, ТАК КАК ТЕПЕРЬ ИСПОЛЬЗУЕТСЯ MoneyMover
        if (moneyEffectPrefab != null)
        {
            Instantiate(moneyEffectPrefab, spawnPosition, Quaternion.identity);
        }
        */
    }

    private void UpdateMoneyText()
    {
        if (moneyText != null)
        {
            moneyText.text = $"Счет: ${currentMoney}";
        }
    }
}