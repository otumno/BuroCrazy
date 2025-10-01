using UnityEngine;

// Этот скрипт - просто "визитка" с контактами всех частей префаба.
// Он ничего не делает сам, только хранит ссылки для других.
public class StaffPrefabReferences : MonoBehaviour
{
    [Header("Визуальные компоненты")]
    public SpriteRenderer bodyRenderer;
    public SpriteRenderer faceRenderer;
    public GameObject nightLight; // <-- Наша цель!

    [Header("Точки крепления")]
    public Transform headAttachPoint;
    public Transform handAttachPoint;
}