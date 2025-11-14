// Файл: SystemsBootstrapper.cs
using UnityEngine;

public class SystemsBootstrapper : MonoBehaviour
{
    void Awake()
    {
        // Проверяем, есть ли у нас родитель. 
        // Если да - отсоединяемся, чтобы стать "корнем".
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        // Делаем этот объект [SYSTEMS] "бессмертным"
        DontDestroyOnLoad(gameObject);
    }
}