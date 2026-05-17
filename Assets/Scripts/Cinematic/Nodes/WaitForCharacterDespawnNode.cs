// === FILE: Assets/Scripts/Cinematic/Nodes/WaitForCharacterDespawnNode.cs ===
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Characters;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел ожидания уничтожения (деспавна) персонажа.
    /// Ждёт, пока персонаж с указанным ключом будет уничтожен, затем переходит к следующему узлу.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Wait For Despawn")]
    public class WaitForCharacterDespawnNode : NextNode
    {
        [Tooltip("Ключ персонажа в CharacterRegistry, событие уничтожения которого ожидаем")]
        public string characterKey;

        public override string GetNodeType() => "wait_despawn";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            if (string.IsNullOrEmpty(characterKey))
            {
                Debug.LogWarning("[WaitForCharacterDespawnNode] characterKey не назначен");
                player.GoToNextNode(nextNode);
                yield break;
            }

            // Проверяем, есть ли персонаж ещё
            var character = CharacterRegistry.Instance.GetCharacter(characterKey);
            if (character == null)
            {
                // Персонаж уже уничтожен или не существовал
                Debug.Log($"[WaitForCharacterDespawnNode] Персонаж {characterKey} уже уничтожен");
                player.GoToNextNode(nextNode);
                yield break;
            }

            Debug.Log($"[WaitForCharacterDespawnNode] Ожидание уничтожения персонажа: {characterKey}");

            // Ждём, пока персонаж не будет уничтожен
            yield return new WaitUntil(() => CharacterRegistry.Instance.GetCharacter(characterKey) == null);

            Debug.Log($"[WaitForCharacterDespawnNode] Персонаж {characterKey} уничтожен, продолжаем");
            player.GoToNextNode(nextNode);
        }
    }
}