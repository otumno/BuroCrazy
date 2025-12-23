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
        
        // Вспомогательный метод для создания узлов в коде (для редактора)
        public T CreateNode<T>() where T : DialogueNode
        {
            T newNode = ScriptableObject.CreateInstance<T>();
            newNode.name = typeof(T).Name;
            newNode.id = System.Guid.NewGuid().ToString();
            allNodes.Add(newNode);
            
            // Важно: добавляем как саб-ассет, чтобы всё хранилось в одном файле
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.AddObjectToAsset(newNode, this);
            UnityEditor.AssetDatabase.SaveAssets();
#endif
            return newNode;
        }
        
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