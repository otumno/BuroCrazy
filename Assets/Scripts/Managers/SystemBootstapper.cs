// Файл: SystemsBootstrapper.cs

using UnityEngine;

namespace Managers
{
    public class SystemsBootstrapper : MonoBehaviour
    {
        private static SystemsBootstrapper s_instance;

        void Awake()
        {
            // [SYSTEMS] лежит в MainMenuScene и создаётся заново при каждом возврате в меню.
            // Уничтожаем дубликат целиком: дочерние синглтоны гасят только свои объекты,
            // а корень с остальным UI остался бы висеть вторым экземпляром.
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;

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
}