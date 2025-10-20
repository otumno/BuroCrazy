using UnityEngine;

// Убедимся, что на объекте с лужей всегда есть коллайдер
[RequireComponent(typeof(Collider2D))]
public class Puddle : MonoBehaviour
{
    [Tooltip("Шанс поскользнуться от 0.0 (0%) до 1.0 (100%)")]
    [Range(0f, 1f)]
    public float slipChance = 0.15f; // 15% шанс по умолчанию

    private void Start()
    {
        // Важно: чтобы триггер сработал, коллайдер должен быть в режиме isTrigger
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
{
    Debug.Log($"[Puddle Trigger] Вошел: {other.name}, Тег: {other.tag}, Слой: {LayerMask.LayerToName(other.gameObject.layer)}");
    AgentMover mover = other.GetComponent<AgentMover>();
    if (mover != null)
    {
        Debug.Log($"Найден AgentMover на {other.name}. Шанс поскользнуться: {slipChance}"); // <<<< Добавь эту строку
        if (Random.value < slipChance)
        {
            Debug.Log($"ПОСКОЛЬЗНУЛСЯ! Вызов SlipAndRecover для {other.name}"); // <<<< Добавь эту строку
            mover.SlipAndRecover();
        }
    }
}
}