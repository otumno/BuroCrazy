// === FILE: Assets/Scripts/Cinematic/Nodes/WaitForSecondsNode.cs ===
using System.Collections;
using UnityEngine;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел ожидания указанного количества секунд.
    /// Использует реальное время (не зависит от Time.timeScale).
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Wait For Seconds")]
    public class WaitForSecondsNode : NextNode
    {
        [Tooltip("Количество секунд ожидания")]
        public float seconds = 1f;
        
        [Tooltip("Использовать реальное время (не зависит от паузы)")]
        public bool useRealtime = true;

        public override string GetNodeType() => "wait";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            if (useRealtime)
                yield return new WaitForSecondsRealtime(seconds);
            else
                yield return new WaitForSeconds(seconds);
                
            player.GoToNextNode(nextNode);
        }
    }
}