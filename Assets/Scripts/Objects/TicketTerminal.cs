using UnityEngine;

namespace Objects
{
    public class TicketTerminal : MonoBehaviour
    {
        public static TicketTerminal Instance { get; private set; }

        [Tooltip("Точка, куда должен подойти клиент для получения талона")]
        public Transform standPoint;

        [Tooltip("Сколько секунд клиент тупит у аппарата")]
        public float interactionTime = 1.5f;

        [Tooltip("Звук печати талона")]
        public AudioClip printSound;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Vector3 GetStandPosition()
        {
            return standPoint != null ? standPoint.position : transform.position;
        }
        
        public Waypoint GetStandWaypoint()
        {
            return standPoint != null ? standPoint.GetComponent<Waypoint>() : GetComponent<Waypoint>();
        }
    }
}
