// === FILE: Assets/Scripts/Cinematic/Nodes/CommentNode.cs ===
using System.Collections;
using UnityEngine;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел комментария.
    /// Не выполняет никаких действий, служит для документации графа.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Comment")]
    public class CommentNode : NextNode
    {
        [TextArea(3, 10)]
        public string comment;

        public override string GetNodeType() => "comment";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            // Ничего не делаем, просто переходим к следующему
            player.GoToNextNode(nextNode);
            yield break;
        }
    }
}