using UnityEngine;
using Characters;

namespace Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public class EntranceTrigger : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            var stateMachine = other.GetComponentInParent<ClientStateMachine>();
            if (stateMachine != null)
            {
                stateMachine.OnEnteredBuilding();
            }
        }
    }
}
