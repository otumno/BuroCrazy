using UnityEngine;

public class StaffPrefabReferences : MonoBehaviour
{
    [Header("Визуальные компоненты")]
    public SpriteRenderer bodyRenderer;
    public SpriteRenderer faceRenderer;
    public GameObject nightLight;

    // --- ИЗМЕНЕНИЕ НАЧАЛО: Добавляем ссылку на эффект ---
    [Tooltip("SpriteRenderer для эффекта повышения уровня")]
    public SpriteRenderer levelUpEffectRenderer; // <<<< НОВОЕ ПОЛЕ
    // --- ИЗМЕНЕНИЕ КОНЕЦ ---

    [Header("Точки крепления")]
    public Transform headAttachPoint;
    public Transform handAttachPoint;
	
	[Header("Звуки")]
	public AudioClip levelUpSound;
}