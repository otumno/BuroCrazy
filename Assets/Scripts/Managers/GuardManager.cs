// Assets/Scripts/Managers/GuardManager.cs
using UnityEngine;

namespace Managers
{
    public class GuardManager : MonoBehaviour
    {
        public static GuardManager Instance { get; private set; }

        public SecurityBarrier securityBarrier;
        public GameObject currentThief;
        public GameObject currentViolator;

        void Awake()
        {
            if (Instance != null) Destroy(gameObject);
            else Instance = this;
        }

        // ИСПРАВЛЕНИЕ: Метод для отчета о нарушителе
        public void ReportViolator(GameObject violator)
        {
            currentViolator = violator;
            // Логика вызова охраны...
        }
    }
}