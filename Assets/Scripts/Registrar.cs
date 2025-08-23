using UnityEngine;

public class Registrar : MonoBehaviour
{
    void Update()
    {
        // Эти два метода теперь полностью контролируют очередь и предотвращают зависания
        ClientQueueManager.CheckForStalledRegistrar();
        ClientQueueManager.CallNextClient();
    }
}