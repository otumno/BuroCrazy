using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Bureau/UI/Character Activity Style")]
public sealed class CharacterActivityStyle : ScriptableObject
{
    public TMP_FontAsset font;
    public Vector3 offset = new Vector3(0f, .65f, 0f);
    public Vector2 panelSize = new Vector2(250f, 82f);
    public float worldScale = .01f;
}
