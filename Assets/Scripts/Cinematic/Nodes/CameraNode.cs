// === FILE: Assets/Scripts/Cinematic/Nodes/CameraNode.cs ===
using System.Collections;
using UnityEngine;
using BuroDebug;
using DG.Tweening;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел управления камерой.
    /// Плавно меняет зум, переключает цель слежки или переходит на DirectorDebugCamera.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Camera")]
    public class CameraNode : NextNode
    {
        [Tooltip("Ключ объекта слежки в SceneObjectRegistry (пусто = не менять)")]
        public string targetKey;
        
        [Tooltip("Размер ортографической камеры (-1 = не менять)")]
        public float orthographicSize = -1f;
        
        [Tooltip("Продолжительность перехода")]
        public float duration = 0.5f;
        
        public Ease ease = Ease.InOutQuad;
        
        [Tooltip("Переключить на DirectorDebugCamera")]
        public bool useDirectorCamera = false;
        
        [Tooltip("Ждать завершения перехода")]
        public bool waitForCompletion = true;

        public override string GetNodeType() => "camera";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[CameraNode] Main camera не найдена");
                player.GoToNextNode(nextNode);
                yield break;
            }

            DirectorDebugCamera directorCam = mainCamera.GetComponent<DirectorDebugCamera>();
            Managers.CameraToggle cameraToggle = mainCamera.GetComponent<Managers.CameraToggle>();

            // Переключение на DirectorDebugCamera
            if (useDirectorCamera && directorCam != null)
            {
                directorCam.SetFollowMode(true);
                if (cameraToggle != null) cameraToggle.enabled = false;
            }
            else if (!useDirectorCamera && cameraToggle != null)
            {
                cameraToggle.enabled = true;
                // ТОЛЬКО сбрасываем follow mode, НЕ отключаем компонент DirectorDebugCamera
                // чтобы клавиша C продолжала работать
                if (directorCam != null)
                {
                    directorCam.SetFollowMode(false);
                }
            }

            // Слежение за объектом и/или зум
            Transform target = null;
            if (!string.IsNullOrEmpty(targetKey))
                target = SceneObjectRegistry.Instance.GetTransform(targetKey);

            if (waitForCompletion && duration > 0)
            {
                float timer = 0f;
                float startSize = mainCamera.orthographicSize;
                float endSize = orthographicSize > 0 ? orthographicSize : startSize;
                Vector3 startPos = mainCamera.transform.position;
                Vector3 endPos = target != null ? target.position : startPos;

                while (timer < duration)
                {
                    timer += Time.deltaTime;
                    float t = Mathf.Clamp01(timer / duration);
                    float easeT = ease switch
                    {
                        Ease.InQuad => t * t,
                        Ease.OutQuad => 1 - (1 - t) * (1 - t),
                        Ease.InOutQuad => t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2,
                        Ease.OutCubic => 1 - Mathf.Pow(1 - t, 3),
                        Ease.InCubic => t * t * t,
                        _ => t
                    };
                    
                    if (orthographicSize > 0)
                        mainCamera.orthographicSize = Mathf.Lerp(startSize, endSize, easeT);
                    if (target != null)
                        mainCamera.transform.position = Vector3.Lerp(startPos, endPos, easeT);
                        
                    yield return null;
                }

                // Финальные значения
                if (orthographicSize > 0)
                    mainCamera.orthographicSize = endSize;
                if (target != null)
                    mainCamera.transform.position = endPos;
            }
            else
            {
                // Мгновенное применение
                if (orthographicSize > 0)
                    mainCamera.orthographicSize = orthographicSize;
                if (target != null)
                    mainCamera.transform.position = target.position;
                    
                yield return null;
            }

            player.GoToNextNode(nextNode);
        }
    }
}