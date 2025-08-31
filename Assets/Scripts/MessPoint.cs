// Файл: MessPoint.cs
using UnityEngine;

/// <summary>
/// Компонент-маркер для любого объекта беспорядка (мусор, лужа, грязь).
/// Сообщает о себе менеджеру MessManager.
/// </summary>
public class MessPoint : MonoBehaviour
{
    public enum MessType 
    { 
        Trash,  // Мусор
        Puddle, // Лужа
        Dirt    // Грязь
    }

    [Tooltip("Тип этого объекта беспорядка.")]
    public MessType type;
    
    [Tooltip("Используется только для грязи, чтобы уборщик знал, сколько времени её убирать.")]
    public int dirtLevel = 1;

    void Start()
    {
        // При появлении на сцене, регистрируемся в центральном менеджере.
        MessManager.Instance?.RegisterMess(this);
    }

    private void OnDestroy()
    {
        // Перед уничтожением, убираем себя из списка менеджера.
        // ?. - это безопасная проверка на случай, если менеджер уже был уничтожен раньше.
        MessManager.Instance?.UnregisterMess(this);
    }
}