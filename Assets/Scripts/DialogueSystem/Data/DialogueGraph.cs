// Файл: Assets/Scripts/DialogueSystem/Data/DialogueGraph.cs
using UnityEngine;
using System.Collections.Generic;

namespace DialogueSystem.Data
{
    [CreateAssetMenu(fileName = "NewDialogue", menuName = "Bureau/Dialogue/Dialogue Graph")]
    public class DialogueGraph : ScriptableObject
    {
        public DialogueNode startNode;
        public List<DialogueNode> allNodes = new List<DialogueNode>();

#if UNITY_EDITOR
        /// <summary>
        /// Список subassets, ожидающих добавления. Позволяет пакетно
        /// добавить ноды и сохранить файл ровно один раз через <see cref="FlushPendingSubassets"/>.
        /// Защищает от потери полей родительского ScriptableObject'а, вызываемого
        /// через SaveAssets() внутри каждого AddObjectToAsset'а.
        /// </summary>
        private static readonly List<System.Collections.Generic.Queue<(DialogueGraph graph, DialogueNode node)>> _pendingBatches
            = new List<System.Collections.Generic.Queue<(DialogueGraph, DialogueNode)>>();
#endif

        // Вспомогательный метод для создания узлов в коде (для редактора)
        public T CreateNode<T>() where T : DialogueNode
        {
            T newNode = ScriptableObject.CreateInstance<T>();
            newNode.name = typeof(T).Name;
            newNode.id = System.Guid.NewGuid().ToString();
            allNodes.Add(newNode);

#if UNITY_EDITOR
            // Добавляем ноду в очередь. Реальная фиксация на диске произойдёт
            // позже, когда внешний код вызовет DialogueGraph.FlushPendingSubassets.
            EnsureBatch(this).Enqueue((this, newNode));
#endif
            return newNode;
        }

        /// <summary>
        /// Регистрирует DialogueGraph'ы, в которых появились pending-ноды.
        /// </summary>
#if UNITY_EDITOR
        private static System.Collections.Generic.Queue<(DialogueGraph, DialogueNode)> EnsureBatch(DialogueGraph graph)
        {
            foreach (var batch in _pendingBatches)
            {
                if (batch.Count > 0 && batch.Peek().Item1 == graph) return batch;
            }
            var newBatch = new System.Collections.Generic.Queue<(DialogueGraph, DialogueNode)>();
            _pendingBatches.Add(newBatch);
            return newBatch;
        }

        /// <summary>
        /// Сохраняет все ожидающие subassets и очищает очереди. Вызывайте
        /// после пакета CreateNode, чтобы избежать многократных SaveAssets()
        /// в середине процесса, которые могут перезатереть изменения родительского
        /// ScriptableObject'а (например, ArcDefinition.arcID).
        /// </summary>
        public static void FlushPendingSubassets()
        {
            foreach (var batch in _pendingBatches)
            {
                while (batch.Count > 0)
                {
                    var (graph, node) = batch.Dequeue();
                    UnityEditor.AssetDatabase.AddObjectToAsset(node, graph);
                    UnityEditor.EditorUtility.SetDirty(graph);
                }
            }
            _pendingBatches.Clear();
            if (UnityEditor.EditorApplication.isPlaying == false)
            {
                UnityEditor.AssetDatabase.SaveAssets();
            }
        }
#endif

        // Метод для удаления узла
        public void DeleteNode(DialogueNode node)
        {
            if (allNodes.Contains(node))
            {
                allNodes.Remove(node);
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.RemoveObjectFromAsset(node);
                UnityEditor.AssetDatabase.SaveAssets();
#endif
            }
        }
    }
}