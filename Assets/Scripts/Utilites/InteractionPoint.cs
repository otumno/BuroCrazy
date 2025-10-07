using UnityEngine;

public class InteractionPoint : MonoBehaviour
{
    public enum InteractionType
    {
        None,
        BarrierControl,
        CollectDocuments,
        WorkAtRegistration,
        WorkAtOfficeDesk,
        WorkAtCashier
    }

    [Tooltip("Тип взаимодействия, который предоставляет эта точка")]
    public InteractionType type = InteractionType.None;

    [Tooltip("(Опционально) Укажите стопку документов, связанную с этой точкой")]
    public DocumentStack associatedStack;

    [Tooltip("(Опционально) Укажите точку, куда должен встать Директор для этого действия")]
    public Transform interactionStandPoint;
}