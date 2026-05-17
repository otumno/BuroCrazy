// === FILE: Assets/Scripts/Cinematic/Nodes/CallCinematicGraphNode.cs ===
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CinematicSystem;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел вызова другого кинематического графа.
    /// Вызывает подграф и ждёт его завершения, затем переходит к следующему узлу.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Call Cinematic Graph")]
    public class CallCinematicGraphNode : NextNode
    {
        [Tooltip("Граф для вызова")]
        public CinematicGraph targetGraph;
        
        [Tooltip("Дополнительные параметры для передачи в граф")]
        public Dictionary<string, object> runtimeParameters;
        
        [Tooltip("Ждать завершения графа перед продолжением")]
        public bool waitForCompletion = true;
        
        [Tooltip("Режим выполнения вызываемого графа")]
        public ExecutionMode executionMode = ExecutionMode.FullControl;

        public override string GetNodeType() => "call_cinematic";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            if (targetGraph == null)
            {
                Debug.LogWarning("[CallCinematicGraphNode] targetGraph не назначен");
                player.GoToNextNode(nextNode);
                yield break;
            }

            // Создаём копию параметров
            if (runtimeParameters != null && runtimeParameters.Count > 0)
            {
                targetGraph.runtimeParameters = new Dictionary<string, object>(runtimeParameters);
            }

            Debug.Log($"[CallCinematicGraphNode] Вызов графа: {targetGraph.name}");

            if (waitForCompletion)
            {
                // Создаём временный плеер для выполнения графа
                var tempPlayer = new GameObject("TempCinematicPlayer").AddComponent<CinematicPlayer>();
                tempPlayer.transform.SetParent(player.transform.parent);
                tempPlayer.gameObject.SetActive(false);

                // Запускаем граф
                var graphRunner = tempPlayer.gameObject.AddComponent<CinematicPlayer>();
                graphRunner.Play(targetGraph, executionMode);

                // Ждём завершения
                yield return new WaitUntil(() => !graphRunner.IsPlaying);

                Debug.Log($"[CallCinematicGraphNode] Граф завершён: {targetGraph.name}");
                
                // Уничтожаем временный плеер
                Destroy(tempPlayer.gameObject);
            }
            else
            {
                // Не ждём - запускаем в фоне
                var bgPlayer = new GameObject("BackgroundCinematicPlayer").AddComponent<CinematicPlayer>();
                bgPlayer.transform.SetParent(player.transform.parent);
                bgPlayer.Play(targetGraph, ExecutionMode.Background);
            }

            player.GoToNextNode(nextNode);
        }
    }
}