// === FILE: Assets/Scripts/Cinematic/Nodes/EndNode.cs ===
using System.Collections;
using UnityEngine;
using Managers;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Конечный узел графа.
    /// Восстанавливает управление, курсор и завершает выполнение графа.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/End")]
    public class EndNode : CinematicNode
    {
        public override string GetNodeType() => "end";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            // Снимаем блокировку управления
            var inputController = FindObjectOfType<PlayerInputController>();
            if (inputController != null)
                inputController.IsCutscenePlaying = false;
            
            // Возвращаем обычный курсор
            var cursor = FindObjectOfType<CursorController>();
            if (cursor != null) cursor.SetCutsceneCursor(false);
            
            // Передаём null чтобы CinematicPlayer завершил выполнение
            player.GoToNextNode(null);
            yield break;
        }
    }
}