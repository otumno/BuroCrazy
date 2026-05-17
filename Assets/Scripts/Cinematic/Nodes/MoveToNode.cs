// === FILE: Assets/Scripts/Cinematic/Nodes/MoveToNode.cs ===
using System.Collections;
using UnityEngine;
using Utilities;
using Characters;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел перемещения персонажа к указанной точке.
    /// Поддерживает pathfinding через вейпоинты или прямое движение.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Move To")]
    public class MoveToNode : NextNode
    {
        [Tooltip("Ключ целевой точки в SceneObjectRegistry")]
        public string targetKey;
        
        [Tooltip("Скорость движения (-1 = текущая)")]
        public float speed = -1f;
        
        [Tooltip("Ждать завершения движения")]
        public bool waitForCompletion = true;
        
        [Tooltip("Использовать Pathfinding по вейпоинтам")]
        public bool usePathfinding = true;
        
        [Tooltip("ID персонажа (Director, Staff:Имя, Client:0)")]
        public string characterID = "Director";

        public override string GetNodeType() => "move";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            // Получаем персонажа
            var character = CharacterRegistry.Instance.GetCharacter(characterID);
            if (character == null)
            {
                Debug.LogError($"[MoveToNode] Персонаж не найден: {characterID}");
                player.GoToNextNode(nextNode);
                yield break;
            }

            var mover = character.GetComponent<AgentMover>();
            if (mover == null)
            {
                Debug.LogError($"[MoveToNode] У персонажа {characterID} нет AgentMover");
                player.GoToNextNode(nextNode);
                yield break;
            }

            // Получаем целевую точку
            var targetTransform = SceneObjectRegistry.Instance.GetTransform(targetKey);
            if (targetTransform == null)
            {
                Debug.LogError($"[MoveToNode] Точка не найдена: {targetKey}");
                player.GoToNextNode(nextNode);
                yield break;
            }

            // Сохраняем исходную скорость, если нужно изменить
            float originalSpeed = mover.moveSpeed;
            if (speed > 0) mover.moveSpeed = speed;

            // Движение
            if (usePathfinding)
            {
                var path = PathfindingUtility.BuildPathTo(mover.transform.position, targetTransform.position, mover.gameObject);
                mover.SetPath(path);
            }
            else
            {
                mover.StartDirectChase(targetTransform.position);
            }

            // Ждём, если нужно
            if (waitForCompletion)
            {
                while (mover.IsMoving())
                    yield return null;
            }
            else
            {
                yield return null;
            }

            // Возвращаем скорость
            if (speed > 0) mover.moveSpeed = originalSpeed;

            player.GoToNextNode(nextNode);
        }
    }
}