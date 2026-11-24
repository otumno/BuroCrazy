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
        private readonly System.Collections.Generic.List<GameObject> violators = new();

        void Awake()
        {
            if (Instance != null) Destroy(gameObject);
            else Instance = this;
        }

        // ИСПРАВЛЕНИЕ: Метод для отчета о нарушителе
        public void ReportViolator(GameObject violator)
        {
            if (violator != null && !violators.Contains(violator)) violators.Add(violator);
            if (currentViolator == null) currentViolator = violator;
        }

        public GameObject GetViolator()
        {
            if (currentViolator != null && !violators.Contains(currentViolator)) violators.Add(currentViolator);
            violators.RemoveAll(v => v == null || !v.activeInHierarchy ||
                v.GetComponent<ClientPathfinding>()?.stateMachine?.GetCurrentState() != ClientState.Enraged);
            currentViolator = violators.Count > 0 ? violators[0] : null;
            return currentViolator;
        }

        public bool PacifyViolator(GameObject violator)
        {
            var client = violator != null ? violator.GetComponent<ClientPathfinding>() : null;
            if (client == null || client.stateMachine == null || client.stateMachine.GetCurrentState() != ClientState.Enraged) return false;
            client.ForceLeave(ClientPathfinding.LeaveReason.CalmedDown);
            violators.Remove(violator);
            if (currentViolator == violator) currentViolator = null;
            Debug.Log($"[GuardTrace] Посетитель {client.name} выдворен");
            return true;
        }

        public void ResetState()
        {
            violators.Clear(); currentViolator = null; currentThief = null;
        }
    }
}
