// Файл: DocumentMover.cs
using UnityEngine;
using System.Collections;
using System;

public class DocumentMover : MonoBehaviour
{
    public float speed = 10f;
    private Transform target;
    private Action onArrival;

    public void StartMove(Transform newTarget, Action onDone = null)
    {
        CancelMove();
        this.target = newTarget;
        this.onArrival = onDone;

        if (this.target != null)
        {
            StartCoroutine(MoveToTarget());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void CancelMove()
    {
        StopAllCoroutines();
        onArrival = null;
        target = null;
    }
    
    // Start() теперь пустой.
    void Start()
    {
    }

    private IEnumerator MoveToTarget()
    {
        transform.SetParent(null, true); 
        
        // --- ИЗМЕНЕНИЕ ЛОГИКИ: Теперь цикл продолжается, пока цель существует ---
        while (target != null && Vector3.Distance(transform.position, target.position) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
            yield return null;
        }
        
        // A disappearing hand/desk is cancellation, not successful delivery.
        var callback = target != null ? onArrival : null;
        if (target != null) transform.position = target.position;
        onArrival = null;
        target = null;
        callback?.Invoke();
        // Мы не уничтожаем объект здесь, чтобы скрипт, вызвавший его, мог это сделать.
    }
}
