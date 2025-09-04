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
        
        onArrival?.Invoke();
        // Мы не уничтожаем объект здесь, чтобы скрипт, вызвавший его, мог это сделать.
    }
}