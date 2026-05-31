// === FILE: Assets/Scripts/Cinematic/Nodes/StartNode.cs ===
using System.Collections;
using UnityEngine;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Стартовый узел графа. Является точкой входа в граф и определяет актора по умолчанию.
    /// Узел указывает characterID - актера, который будет использоваться последующими узлами,
    /// если в них не указан свой идентификатор или используется маркер "$Actor".
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Start")]
    public class StartNode : CinematicNode
    {
        /// <summary>Следующий узел после старта</summary>
        public CinematicNode nextNode;
        
        /// <summary>
        /// ID актера для этого графа. Этот актер будет использоваться по умолчанию
        /// для всех последующих узлов, если в них не указан свой characterID
        /// или используется маркер "$Actor".
        /// </summary>
        [Tooltip("ID актера. Будет использоваться узлами как DefaultActor, если не указан свой.")]
        public string characterID = "Director";

        public override string GetNodeType() => "start";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            Debug.Log($"[StartNode] Execute: this={this?.name ?? "null"}, characterID={characterID}");
            Debug.Log($"[StartNode] Execute: nextNode is null? {nextNode == null}");
            if (nextNode != null)
            {
                Debug.Log($"[StartNode] Execute: nextNode.name='{nextNode.name}', nextNode.nodeName='{nextNode.nodeName}', nextNode.id='{nextNode.id}'");
            }
            // Устанавливаем DefaultActor для игрока из этого узла
            player.DefaultActor = characterID;
            Debug.Log($"[StartNode] Вызываю GoToNextNode с nextNode");
            player.GoToNextNode(nextNode);
            yield break;
        }
    }
}