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
	
	public GameObject processingIconPrefab;

	   [Header("Несгораемые префабы (для StaffController)")]
	   public GameObject successEffectPrefab;
	   public GameObject failureEffectPrefab;
	   public GameObject puddlePrefab;
	   public GameObject trashPrefab;
	   public GameObject mudPrefab;
	  
	   [Header("Точки вылета иконок (в руках)")]
	   public Transform successSpawnPoint;
	   public Transform failureSpawnPoint;

	   [Header("Звуковые ID (из SoundID)")]
	   public Scriptables.Audio.SoundID successSoundID = Scriptables.Audio.SoundID.None;
	   public Scriptables.Audio.SoundID failureSoundID = Scriptables.Audio.SoundID.None;
	  }