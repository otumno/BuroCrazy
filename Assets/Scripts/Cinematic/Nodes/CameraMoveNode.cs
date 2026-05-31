// === FILE: Assets/Scripts/Cinematic/Nodes/CameraMoveNode.cs ===
using System.Collections;
using System.Reflection;
using UnityEngine;
using Managers;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Кинематическая нода для плавного перемещения камеры.
    /// Поддерживает различные типы easing, паузу в конце и возврат к исходным параметрам.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Camera Move")]
    public class CameraMoveNode : NextNode
    {
        /// <summary>
        /// Источник начальной позиции камеры.
        /// </summary>
        public enum PositionSource
        {
            CurrentCamera,  // Использовать текущую позицию камеры
            StandardCamera, // Позиция и размер из стандартной камеры (CameraToggle)
            Transform       // Использовать позицию указанного Transform
        }

        /// <summary>
        /// Тип функции сглаживания (easing).
        /// </summary>
        public enum EaseTypeEnum
        {
            Linear,
            InQuad,
            OutQuad,
            InOutQuad,
            InCubic,
            OutCubic,
            InOutCubic,
            SmoothStep
        }

        [Header("Начальная позиция")]
        [Tooltip("Источник начальной позиции камеры")]
        public PositionSource startPositionSource = PositionSource.CurrentCamera;

        [Tooltip("Transform для начальной позиции (если выбран Transform)")]
        public Transform startTransform;

        [Tooltip("Индекс позиции в CameraToggle (если источник StandardCamera, -1 = текущая активная)")]
        public int standardCameraPositionIndex = -1;

        [Tooltip("Начальный orthographic size (если источник Transform)")]
        public float startOrthographicSize = 5f;

        [Header("Конечная позиция")]
        [Tooltip("Источник конечной позиции камеры")]
        public PositionSource endPositionSource = PositionSource.Transform;

        [Tooltip("Transform для конечной позиции")]
        public Transform endTransform;

        [Tooltip("Конечный orthographic size")]
        public float endOrthographicSize = 5f;

        [Header("Настройки анимации")]
        [Tooltip("Время перемещения в секундах")]
        public float duration = 1f;

        [Tooltip("Тип функции сглаживания")]
        public EaseTypeEnum easeType = EaseTypeEnum.InOutQuad;

        [Tooltip("Делать паузу после перемещения")]
        public bool pauseAtEnd = false;

        [Tooltip("Длительность паузы в секундах")]
        public float pauseDuration = 0.5f;

        [Tooltip("Время возврата к исходным параметрам")]
        public float returnDuration = 0.5f;

        public override string GetNodeType() => "camera_move";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            // Проверяем наличие камеры
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[CameraMoveNode] Камера не найдена (Camera.main == null)");
                player.GoToNextNode(nextNode);
                yield break;
            }

            // Запоминаем исходное состояние камеры
            Vector3 originalPosition = cam.transform.position;
            float originalOrthographicSize = cam.orthographicSize;

            // Определяем начальную позицию
            Vector3 startPosition;
            float startOrthoSize;
            if (startPositionSource == PositionSource.Transform && startTransform != null)
            {
                startPosition = startTransform.position;
                startOrthoSize = startOrthographicSize;
            }
            else if (startPositionSource == PositionSource.StandardCamera)
            {
                GetStandardCameraPosition(out startPosition, out startOrthoSize);
            }
            else
            {
                startPosition = originalPosition;
                startOrthoSize = originalOrthographicSize;
            }

            // Определяем конечную позицию
            Vector3 endPosition;
            float endOrthoSize;
            if (endPositionSource == PositionSource.Transform && endTransform != null)
            {
                endPosition = endTransform.position;
                endOrthoSize = endOrthographicSize;
            }
            else if (endPositionSource == PositionSource.StandardCamera)
            {
                GetStandardCameraPosition(out endPosition, out endOrthoSize);
            }
            else if (endPositionSource == PositionSource.CurrentCamera)
            {
                endPosition = originalPosition;
                endOrthoSize = originalOrthographicSize;
            }
            else
            {
                Debug.LogWarning("[CameraMoveNode] EndTransform не назначен, используется текущая позиция");
                endPosition = cam.transform.position;
                endOrthoSize = cam.orthographicSize;
            }

            // Устанавливаем начальную позицию
            cam.transform.position = startPosition;
            if (cam.orthographic)
            {
                cam.orthographicSize = startOrthoSize;
            }

            // Выполняем анимацию перемещения к конечной точке
            yield return AnimateCamera(cam, startPosition, endPosition, startOrthoSize, endOrthoSize, duration, easeType);

            // Проверяем, не была ли камера уничтожена
            if (cam == null)
            {
                Debug.LogWarning("[CameraMoveNode] Камера была уничтожена во время анимации");
                yield break;
            }

            // Пауза в конце, если указано
            if (pauseAtEnd)
            {
                yield return new WaitForSecondsRealtime(pauseDuration);
                
                // Проверяем камеру после паузы
                if (cam == null)
                {
                    yield break;
                }
            }

            // Возвращаем камеру к исходным параметрам
            yield return AnimateCamera(cam, cam.transform.position, originalPosition, cam.orthographicSize, originalOrthographicSize, returnDuration, easeType);

            // Переходим к следующему узлу
            player.GoToNextNode(nextNode);
        }

        /// <summary>
        /// Получает позицию и размер стандартной камеры из CameraToggle.
        /// </summary>
        private void GetStandardCameraPosition(out Vector3 position, out float orthoSize)
        {
            position = Vector3.zero;
            orthoSize = 5f;

            CameraToggle cameraToggle = Camera.main?.GetComponent<CameraToggle>();
            if (cameraToggle == null)
            {
                Debug.LogWarning("[CameraMoveNode] CameraToggle не найден на Camera.main");
                return;
            }

            // Получаем доступ к приватному полю _positions через reflection
            var positionsField = typeof(CameraToggle).GetField("_positions",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (positionsField != null)
            {
                var positions = positionsField.GetValue(cameraToggle) as Transform[];
                if (positions != null && positions.Length > 0)
                {
                    // Если задан индекс и он валиден - используем его
                    if (standardCameraPositionIndex >= 0 && standardCameraPositionIndex < positions.Length)
                    {
                        position = positions[standardCameraPositionIndex].position;
                    }
                    else
                    {
                        // Иначе используем текущую позицию камеры CameraToggle
                        var cameraField = typeof(CameraToggle).GetField("_camera",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (cameraField != null)
                        {
                            var camObj = cameraField.GetValue(cameraToggle) as Camera;
                            if (camObj != null)
                            {
                                position = camObj.transform.position;
                            }
                        }
                    }
                }
            }

            // Получаем orthographic size
            var orthoSizeField = typeof(CameraToggle).GetField("_defaultOrthographicSize",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (orthoSizeField != null)
            {
                orthoSize = (float)orthoSizeField.GetValue(cameraToggle);
            }

            Debug.Log($"[CameraMoveNode] StandardCamera: position={position}, orthoSize={orthoSize}, index={standardCameraPositionIndex}");
        }

        /// <summary>
        /// Анимирует камеру от начальной позиции к конечной.
        /// </summary>
        private IEnumerator AnimateCamera(Camera cam, Vector3 startPos, Vector3 endPos, float startOrthoSize, float endOrthoSize, float duration, EaseTypeEnum easeType)
        {
            if (duration <= 0f)
            {
                // Если duration == 0, сразу перемещаем
                if (cam != null)
                {
                    cam.transform.position = endPos;
                    if (cam.orthographic)
                    {
                        cam.orthographicSize = endOrthoSize;
                    }
                }
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Проверяем, что камера ещё существует
                if (cam == null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                
                // Применяем easing
                float easedT = EasingHelper.ApplyEasing(t, easeType);

                // Интерполируем позицию
                cam.transform.position = Vector3.Lerp(startPos, endPos, easedT);

                // Интерполируем orthographic size
                if (cam.orthographic)
                {
                    cam.orthographicSize = Mathf.Lerp(startOrthoSize, endOrthoSize, easedT);
                }

                yield return null;
            }

            // Гарантируем финальную позицию
            if (cam != null)
            {
                cam.transform.position = endPos;
                if (cam.orthographic)
                {
                    cam.orthographicSize = endOrthoSize;
                }
            }
        }
    }

    /// <summary>
    /// Вспомогательный класс для функций сглаживания (easing).
    /// </summary>
    public static class EasingHelper
    {
        /// <summary>
        /// Применяет функцию easing к значению t (0..1).
        /// </summary>
        public static float ApplyEasing(float t, CameraMoveNode.EaseTypeEnum easeType)
        {
            return easeType switch
            {
                CameraMoveNode.EaseTypeEnum.Linear => Linear(t),
                CameraMoveNode.EaseTypeEnum.InQuad => InQuad(t),
                CameraMoveNode.EaseTypeEnum.OutQuad => OutQuad(t),
                CameraMoveNode.EaseTypeEnum.InOutQuad => InOutQuad(t),
                CameraMoveNode.EaseTypeEnum.InCubic => InCubic(t),
                CameraMoveNode.EaseTypeEnum.OutCubic => OutCubic(t),
                CameraMoveNode.EaseTypeEnum.InOutCubic => InOutCubic(t),
                CameraMoveNode.EaseTypeEnum.SmoothStep => SmoothStep(t),
                _ => Linear(t)
            };
        }

        /// <summary>
        /// Линейная интерполяция (без сглаживания).
        /// </summary>
        public static float Linear(float t)
        {
            return t;
        }

        /// <summary>
        /// Квадратичное сглаживание на вход (замедление в начале).
        /// </summary>
        public static float InQuad(float t)
        {
            return t * t;
        }

        /// <summary>
        /// Квадратичное сглаживание на выход (замедление в конце).
        /// </summary>
        public static float OutQuad(float t)
        {
            return t * (2f - t);
        }

        /// <summary>
        /// Квадратичное сглаживание на вход и выход (замедление в начале и конце).
        /// </summary>
        public static float InOutQuad(float t)
        {
            if (t < 0.5f)
            {
                return 2f * t * t;
            }
            return -1f + (4f - 2f * t) * t;
        }

        /// <summary>
        /// Кубическое сглаживание на вход.
        /// </summary>
        public static float InCubic(float t)
        {
            return t * t * t;
        }

        /// <summary>
        /// Кубическое сглаживание на выход.
        /// </summary>
        public static float OutCubic(float t)
        {
            float f = t - 1f;
            return f * f * f + 1f;
        }

        /// <summary>
        /// Кубическое сглаживание на вход и выход.
        /// </summary>
        public static float InOutCubic(float t)
        {
            if (t < 0.5f)
            {
                return 4f * t * t * t;
            }
            float f = 2f * t - 2f;
            return 0.5f * f * f * f + 1f;
        }

        /// <summary>
        /// Гладкое сглаживание ( SmoothStep ).
        /// </summary>
        public static float SmoothStep(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }
    }
}