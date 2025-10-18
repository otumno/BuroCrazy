// Файл: HoverToggleObject.cs
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HoverToggleObject : MonoBehaviour
{
    [Tooltip("Объект, который нужно включать/выключать при наведении (например, Body_Outline)")]
    public GameObject objectToToggle;

    private void OnMouseEnter()
    {
        if (objectToToggle != null)
        {
            objectToToggle.SetActive(true);
        }
    }

    private void OnMouseExit()
    {
        if (objectToToggle != null)
        {
            objectToToggle.SetActive(false);
        }
    }
}