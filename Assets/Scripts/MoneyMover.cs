using UnityEngine;
using System.Collections;

public class MoneyMover : MonoBehaviour
{
    public Transform target;
    public float minSpeed = 8f;   // Минимальная скорость
    public float maxSpeed = 12f;  // Максимальная скорость
    public float arrivalThreshold = 0.1f;

    private float currentSpeed; // Скорость конкретно этой купюры

    void Start()
    {
        // Выбираем случайную скорость из диапазона
        currentSpeed = Random.Range(minSpeed, maxSpeed);

        if (target != null)
        {
            StartCoroutine(MoveToTarget());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator MoveToTarget()
    {
        while (Vector3.Distance(transform.position, target.position) > arrivalThreshold)
        {
            // Используем выбранную случайную скорость
            transform.position = Vector3.MoveTowards(transform.position, target.position, currentSpeed * Time.deltaTime);
            yield return null;
        }

        Destroy(gameObject);
    }
}