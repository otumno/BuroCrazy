// Assets/Scripts/Gameplay/SecurityBarrier.cs
using UnityEngine;

public class SecurityBarrier : MonoBehaviour
{
    public Transform interactionPoint; 
    public Transform guardInteractionPoint => interactionPoint; 

    public bool IsActive() 
    { 
        return gameObject.activeSelf; 
    }

    public void ActivateBarrier() { gameObject.SetActive(true); }
    public void DeactivateBarrier() { gameObject.SetActive(false); }

    // --- ДОБАВЬТЕ ЭТОТ МЕТОД ---
    public void ToggleBarrier()
    {
        if (IsActive()) DeactivateBarrier();
        else ActivateBarrier();
    }
}