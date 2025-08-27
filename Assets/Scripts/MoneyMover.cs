using UnityEngine;
using System.Collections;
using System;

public class MoneyMover : MonoBehaviour
{
    public float minSpeed = 8f;
    public float maxSpeed = 12f;
    
    private Transform target;
    private float currentSpeed;

    public void StartMove(Transform newTarget)
    {
        this.target = newTarget;
        this.currentSpeed = UnityEngine.Random.Range(minSpeed, maxSpeed);

        if (this.target != null)
        {
            StartCoroutine(MoveToTarget());
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    // Start() теперь пустой.
    void Start()
    {
    }

    private IEnumerator MoveToTarget()
    {
        while (Vector3.Distance(transform.position, target.position) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target.position, currentSpeed * Time.deltaTime);
            yield return null;
        }
        Destroy(gameObject);
    }
}