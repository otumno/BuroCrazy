using UnityEngine;

public class ClickMarkerFeedback : MonoBehaviour
{
    // Время жизни объекта в секундах
    public float lifetime = 1.0f;

    void Start()
    {
        // Уничтожить этот GameObject через 'lifetime' секунд
        Destroy(gameObject, lifetime);
    }
}