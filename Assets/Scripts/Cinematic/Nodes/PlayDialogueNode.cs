using UnityEngine;
using System.Collections;
using DialogueSystem.Data;
using Managers;

namespace CinematicSystem.Nodes
{
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Play Dialogue")]
    public class PlayDialogueNode : NextNode
    {
        [SerializeField] public DialogueGraph dialogue;
        
        public override string GetNodeType() => "play_dialogue";
        
        public override IEnumerator Execute(CinematicPlayer player)
        {
            if (dialogue == null)
            {
                Debug.LogWarning("[PlayDialogueNode] Dialogue not assigned!");
                yield break;
            }
            
            bool dialogueEnded = false;
            
            DialogueUIManager.Instance.StartDialogue(dialogue, null, () =>
            {
                dialogueEnded = true;
            });
            
            yield return new WaitUntil(() => dialogueEnded);
            
            player.GoToNextNode(nextNode);
        }
    }
}