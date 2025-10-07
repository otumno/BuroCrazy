using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EnvelopeStack : MonoBehaviour
{
    [Header("Настройки стопки конвертов")]
    [Tooltip("Максимальное количество конвертов в стопке.")]
    public int maxEnvelopes = 50;
    [Tooltip("Префаб одного конверта для визуализации стопки.")]
    public GameObject envelopeVisualPrefab;
    [Tooltip("Вертикальное смещение для каждого нового конверта.")]
    public float stackOffset = 0.05f;

    private List<GameObject> visualStack = new List<GameObject>();

    public int CurrentEnvelopeCount => visualStack.Count;
    public bool IsFull => CurrentEnvelopeCount >= maxEnvelopes;
    public bool IsEmpty => CurrentEnvelopeCount == 0;

    /// <summary>
    /// Добавляет один конверт в стопку. Возвращает true в случае успеха.
    /// </summary>
    public bool AddEnvelope()
    {
        if (IsFull || envelopeVisualPrefab == null)
        {
            return false;
        }

        Vector3 position = transform.position + new Vector3(0, CurrentEnvelopeCount * stackOffset, 0);
        GameObject newEnvelope = Instantiate(envelopeVisualPrefab, position, transform.rotation, transform);
        visualStack.Add(newEnvelope);
        return true;
    }

    /// <summary>
    /// Сотрудник забирает один конверт. Возвращает true в случае успеха.
    /// </summary>
    public bool TakeOneEnvelope()
    {
        if (IsEmpty) return false;

        GameObject envelopeToRemove = visualStack.Last();
        visualStack.Remove(envelopeToRemove);
        Destroy(envelopeToRemove);
        return true;
    }

    /// <summary>
    /// Очищает всю стопку (например, для новой игры).
    /// </summary>
    public void ClearStack()
    {
        foreach (var envelope in visualStack)
        {
            Destroy(envelope);
        }
        visualStack.Clear();
    }
}