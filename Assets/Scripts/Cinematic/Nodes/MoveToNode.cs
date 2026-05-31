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
            Debug.Log($"[MoveToNode] >>> Выполняю узел: {GetNodeType()} targetKey={targetKey} characterID={characterID}");

            // Получаем персонажа
            var character = CharacterRegistry.Instance.GetCharacter(characterID);
            if (character == null)
            {
                Debug.LogError($"[MoveToNode] Персонаж не найден: {characterID}");
                player.GoToNextNode(nextNode);
                yield break;
            }
            Debug.Log($"[MoveToNode] Персонаж найден: {character.name}");

            var mover = character.GetComponent<AgentMover>();
            if (mover == null)
            {
                Debug.LogError($"[MoveToNode] У персонажа {characterID} нет AgentMover");
                player.GoToNextNode(nextNode);
                yield break;
            }
            Debug.Log($"[MoveToNode] AgentMover найден, IsMoving={mover.IsMoving()}");

            // Получаем целевую точку
            var targetTransform = SceneObjectRegistry.Instance.GetTransform(targetKey);
            if (targetTransform == null)
            {
                Debug.LogError($"[MoveToNode] Точка не найдена: {targetKey}");
                player.GoToNextNode(nextNode);
                yield break;
            }
            Debug.Log($"[MoveToNode] TargetTransform найден: {targetTransform.name} at {targetTransform.position}");

            // Проверяем расстояние до цели
            float distanceToTarget = Vector2.Distance(character.transform.position, targetTransform.position);
            Debug.Log($"[MoveToNode] Расстояние до цели: {distanceToTarget}");
            
            // Если уже у цели - сразу завершаем
            if (distanceToTarget < 0.5f)
            {
                Debug.Log($"[MoveToNode] Персонаж уже у цели (расстояние={distanceToTarget}), пропускаем движение");
                player.GoToNextNode(nextNode);
                yield break;
            }

            // Сохраняем исходную скорость, если нужно изменить
            float originalSpeed = mover.moveSpeed;
            if (speed > 0) mover.moveSpeed = speed;

            // Движение
            bool pathEstablished = false;
            if (usePathfinding)
            {
                Debug.Log($"[MoveToNode] Использую Pathfinding, start={mover.transform.position}");
                var path = PathfindingUtility.BuildPathTo(mover.transform.position, targetTransform.position, mover.gameObject);
                
                if (path != null && path.Count > 0)
                {
                    mover.SetPath(path);
                    pathEstablished = true;
                    Debug.Log($"[MoveToNode] Path установлен, path.Count={path.Count}");
                }
                else
                {
                    Debug.LogWarning($"[MoveToNode] Pathfinding вернул пустой путь, использую DirectChase");
                }
            }
            
            // Если pathfinding не удался или отключен - используем DirectChase
            if (!pathEstablished)
            {
                Debug.Log($"[MoveToNode] Использую DirectChase к {targetTransform.position}");
                mover.StartDirectChase(targetTransform.position);
            }

            // Ждём, если нужно
            if (waitForCompletion)
            {
                Debug.Log($"[MoveToNode] Ожидание завершения движения...");
                int frameCount = 0;
                int minFramesToWait = 10; // Минимум 10 кадров для старта движения
                
                while (mover.IsMoving() || frameCount < minFramesToWait)
                {
                    frameCount++;
                    yield return null;
                    
                    // Если прошло достаточно кадров но IsMoving=false - проверяем расстояние
                    if (frameCount >= minFramesToWait && !mover.IsMoving())
                    {
                        float dist = Vector2.Distance(character.transform.position, targetTransform.position);
                        if (dist < 1.0f)
                        {
                            Debug.Log($"[MoveToNode] При接近 цели (dist={dist}), завершаем ожидание");
                            break;
                        }
                        // Если далеко от цели но не двигается - что-то пошло не так
                        Debug.LogWarning($"[MoveToNode] Не двигаемся (frame={frameCount}, dist={dist}), продолжаем ждать...");
                    }
                }
                
                // Дополнительная проверка: возможно мы остановились не у цели
                float finalDistance = Vector2.Distance(character.transform.position, targetTransform.position);
                Debug.Log($"[MoveToNode] Движение завершено за {frameCount} кадров, финальное расстояние={finalDistance}");
            }
            else
            {
                Debug.Log($"[MoveToNode] Не жду завершения движения");
                yield return null;
            }

            // Возвращаем скорость
            if (speed > 0) mover.moveSpeed = originalSpeed;

            Debug.Log($"[MoveToNode] Завершаю узел, перехожу к nextNode");
            player.GoToNextNode(nextNode);
        }
    }
}