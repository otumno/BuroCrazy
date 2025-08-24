using UnityEngine;

// Этот скрипт нужно повесить на объект с 2D-коллайдером (например, BoxCollider2D),
// у которого включена галочка 'Is Trigger'.
public class DestroyZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Проверяем, есть ли у вошедшего объекта один из скриптов персонажей
        if (other.GetComponent<ClientPathfinding>() != null ||
            other.GetComponent<ClerkController>() != null ||
            other.GetComponent<GuardMovement>() != null)
        {
            // Если да, уничтожаем корневой объект этого персонажа
            Destroy(other.gameObject);
        }
    }
}