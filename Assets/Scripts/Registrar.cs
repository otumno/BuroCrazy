using UnityEngine;

public class Registrar : MonoBehaviour
{
    void Update()
    {
        // Обращаемся к методам через синглтон
        ClientQueueManager.Instance.CheckForStalledRegistrar();
        ClientQueueManager.Instance.CallNextClient();
    }
}