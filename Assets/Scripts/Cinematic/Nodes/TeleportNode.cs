// === FILE: Assets/Scripts/Cinematic/Nodes/TeleportNode.cs ===
using System.Collections;
using UnityEngine;
using Characters;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел мгновенной телепортации персонажа в указанную точку.
    /// Перемещает персонажа без анимации движения.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Teleport")]
    public class TeleportNode : NextNode
    {
        [Tooltip("Ключ персонажа (Director, Staff:Имя, Client:0)")]
        public string characterID = "Director";
        
        [Tooltip("Ключ целевой точки в SceneObjectRegistry")]
        public string targetKey;
        
        [Tooltip("Сохранять поворот персонажа")]
        public bool keepRotation = true;

        public override string GetNodeType() => "teleport";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            // Получаем персонажа
            var character = CharacterRegistry.Instance.GetCharacter(characterID);
            if (character == null)
            {
                Debug.LogError($"[TeleportNode] Персонаж не найден: {characterID}");
                player.GoToNextNode(nextNode);
                yield break;
            }

            // Получаем целевую точку
            var targetTransform = SceneObjectRegistry.Instance.GetTransform(targetKey);
            if (targetTransform == null)
            {
                Debug.LogError($"[TeleportNode] Точка не найдена: {targetKey}");
                player.GoToNextNode(nextNode);
                yield break;
            }

            // Сохраняем поворот если нужно
            Quaternion rotation = keepRotation ? character.transform.rotation : Quaternion.identity;
            
            // Телепортируем
            character.transform.position = targetTransform.position;
            
            if (keepRotation)
            {
                character.transform.rotation = rotation;
            }
            
            // Останавливаем движение если есть AgentMover
            if (character.TryGetComponent<AgentMover>(out var mover))
            {
                mover.Stop();
            }

            player.GoToNextNode(nextNode);
        }
    }
}