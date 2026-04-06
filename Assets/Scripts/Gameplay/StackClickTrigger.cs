using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using Managers;
using Scriptables.Audio;

[RequireComponent(typeof(DocumentStack), typeof(BoxCollider2D))]
public class StackClickTrigger : MonoBehaviour
{
    [Header("Звуки")]
    public SoundID soundIdOnClick = SoundID.UI_Click_Default;
    public SoundID soundIdOnHover = SoundID.UI_Hover;

    [Header("Материалы подсветки")]
    [Tooltip("Материал для свечения (например, Mat_HighlightAdditive)")]
    [SerializeField] public Material highlightMaterial; // Сделали явно публичным и сериализуемым
    
    [Tooltip("Прозрачность свечения при наведении")]
    [Range(0f, 1f)]
    public float highlightAlpha = 0.5f;

    private Material _originalMaterial;
    private DocumentStack _myStack;

    private static readonly string[] collectReplics = {
        "Слишком много макулатуры. Отнесу в архив.",
        "Помогу ребятам разгрести этот завал.",
        "Не стол, а свалка. Забираю."
    };

    private void Awake()
    {
        _myStack = GetComponent<DocumentStack>();
        var col = GetComponent<BoxCollider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnMouseEnter()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!CanInteract()) return;

        if (soundIdOnHover != SoundID.None && AudioManager.Instance != null)
            AudioManager.Instance.PlaySound(soundIdOnHover, transform.position);

        SetStackMaterial(highlightMaterial, true);
    }

    private void OnMouseExit()
    {
        SetStackMaterial(_originalMaterial, false);
    }

    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!CanInteract()) return;

        StartCoroutine(ExecuteClickWithDelay());
    }

    private IEnumerator ExecuteClickWithDelay()
    {
        yield return null; 

        if (soundIdOnClick != SoundID.None && AudioManager.Instance != null)
            AudioManager.Instance.PlaySound(soundIdOnClick, transform.position);

        SetStackMaterial(_originalMaterial, false);

        DirectorAvatarController.Instance.CollectDocuments(_myStack);
        
        string randomReplic = collectReplics[Random.Range(0, collectReplics.Length)];
        DirectorAvatarController.Instance.thoughtBubble?.ShowPriorityMessage(randomReplic, 3.5f, Color.white);
    }

    private bool CanInteract()
    {
        if (DirectorAvatarController.Instance == null || _myStack == null) return false;
        if (_myStack.IsEmpty) return false;

        var state = DirectorAvatarController.Instance.GetCurrentState();
        if (state != DirectorAvatarController.DirectorState.Idle && state != DirectorAvatarController.DirectorState.AtDesk) 
            return false;

        if (DirectorAvatarController.Instance.IsInUninterruptibleAction) return false;

        return true;
    }

    private void SetStackMaterial(Material mat, bool isHighlighting)
    {
        if (mat == null) return;
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        
        foreach (var sr in renderers)
        {
            if (isHighlighting && _originalMaterial == null) _originalMaterial = sr.sharedMaterial; 
            
            sr.sharedMaterial = mat;
            Color c = sr.color;
            c.a = isHighlighting ? highlightAlpha : 1f; 
            sr.color = c;
        }
    }
}
