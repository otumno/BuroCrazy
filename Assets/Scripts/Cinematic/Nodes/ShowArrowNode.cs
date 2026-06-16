using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using CinematicSystem;

namespace CinematicSystem.Nodes
{
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Show Arrow")]
    public class ShowArrowNode : NextNode
    {
        [Tooltip("Ключ объекта-стрелки в SceneObjectRegistry (например 'DirectorDeskHintArrow').")]
        public string arrowKey;

        [Tooltip("Ключ кнопки в SceneObjectRegistry, по которой игрок должен кликнуть, чтобы продолжить (например 'DeskButton'). Если пусто — ожидается любой клик мыши.")]
        public string buttonKey;

        [Tooltip("Таймаут ожидания клика (0 = бесконечно)")]
        public float timeout = 0f;

        public override string GetNodeType() => "show_arrow";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            Debug.Log($"[ShowArrowNode] Execute START. arrowKey='{arrowKey}', buttonKey='{buttonKey}'");

            // 0. Переключаем курсор на обычный, чтобы игрок видел, что можно кликать.
            CursorController.Instance?.SetCutsceneCursor(false);

            // 1. Найти стрелку (с ожиданием появления до 1 секунды — UI может инициализироваться с задержкой)
            GameObject hintArrow = null;
            float waitTimer = 0f;
            const float maxWait = 1.0f;
            while (hintArrow == null && waitTimer < maxWait)
            {
                hintArrow = ResolveArrow();
                if (hintArrow == null)
                {
                    yield return new WaitForSecondsRealtime(0.1f);
                    waitTimer += 0.1f;
                }
            }

            if (hintArrow != null)
            {
                hintArrow.SetActive(true);
                Debug.Log($"[ShowArrowNode] Arrow shown: name='{hintArrow.name}', activeInHierarchy={hintArrow.activeInHierarchy}");
            }
            else
            {
                Debug.LogWarning($"[ShowArrowNode] Arrow NOT FOUND after {maxWait}с wait. arrowKey='{arrowKey}'. " +
                                 $"SceneObjectRegistry.Instance={SceneObjectRegistry.Instance != null}. " +
                                 $"Active scene='{SceneManager.GetActiveScene().name}'");
            }

            // 2. Ждём клик по нужной кнопке (или любой клик, если ключ кнопки не задан)
            yield return WaitForTargetClick();

            // 3. Скрываем стрелку после клика
            if (hintArrow != null)
            {
                hintArrow.SetActive(false);
                Debug.Log("[ShowArrowNode] Arrow hidden");
            }

            // 4. Если катсцена ещё играется — возвращаем катсценный курсор.
            if (player != null)
            {
                CursorController.Instance?.SetCutsceneCursor(true);
            }

            Debug.Log("[ShowArrowNode] Execute END, going to nextNode");
            player.GoToNextNode(nextNode);
        }

        private GameObject ResolveArrow()
        {
            // Соберём список имён для поиска: сначала явный ключ, потом fallback-список
            var searchNames = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(arrowKey)) searchNames.Add(arrowKey);
            // Fallback-имена, известные из проекта
            searchNames.Add("DirectorDeskHintArrow");
            searchNames.Add("UIGOArrow");
            searchNames.Add("HintArrow");

            // Способ 1: Через SceneObjectRegistry по строковому ключу
            if (SceneObjectRegistry.Instance != null)
            {
                foreach (var name in searchNames)
                {
                    var go = SceneObjectRegistry.Instance.GetGameObject(name);
                    if (go != null)
                    {
                        Debug.Log($"[ShowArrowNode] Resolved via SceneObjectRegistry: '{go.name}' (key='{name}')");
                        return go;
                    }
                }
            }

            // Способ 2: Поиск в активной сцене по точному имени
            foreach (var name in searchNames)
            {
                var found = GameObject.Find(name);
                if (found != null)
                {
                    Debug.Log($"[ShowArrowNode] Resolved via GameObject.Find: '{found.name}'");
                    return found;
                }
            }

            // Способ 3: В корне сцены и среди дочерних объектов по подстроке
            GameObject[] allObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var name in searchNames)
            {
                foreach (GameObject obj in allObjects)
                {
                    if (obj.name == name)
                    {
                        Debug.Log($"[ShowArrowNode] Resolved via root scan (exact): '{obj.name}'");
                        return obj;
                    }
                    if (obj.name.Contains(name))
                    {
                        Debug.Log($"[ShowArrowNode] Resolved via root scan (contains): '{obj.name}'");
                        return obj;
                    }
                    // Проверим дочерние объекты (включая неактивные)
                    var found = FindInChildren(obj.transform, name);
                    if (found != null)
                    {
                        Debug.Log($"[ShowArrowNode] Resolved via child scan: '{found.name}' inside '{obj.name}'");
                        return found.gameObject;
                    }
                }
            }

            // Способ 4: В StartOfDayPanel через reflection
            var startOfDay = Object.FindObjectOfType<StartOfDayPanel>();
            if (startOfDay != null)
            {
                var field = startOfDay.GetType().GetField("hintArrow",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var go = field.GetValue(startOfDay) as GameObject;
                    if (go != null)
                    {
                        Debug.Log($"[ShowArrowNode] Resolved via StartOfDayPanel.hintArrow: '{go.name}'");
                        return go;
                    }
                }
            }

            // Способ 5: Поиск по тегу (legacy fallback)
            try
            {
                var byTag = GameObject.FindWithTag("HintArrow");
                if (byTag != null)
                {
                    Debug.Log($"[ShowArrowNode] Resolved via tag 'HintArrow': '{byTag.name}'");
                    return byTag;
                }
            }
            catch { /* тег может быть не определён */ }

            return null;
        }

        private static Transform FindInChildren(Transform parent, string name)
        {
            // Рекурсивный поиск по имени (включая неактивные)
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name || child.name.Contains(name))
                    return child;
                var deeper = FindInChildren(child, name);
                if (deeper != null) return deeper;
            }
            return null;
        }

        private IEnumerator WaitForTargetClick()
        {
            // Соберём список ключей кнопки
            var searchKeys = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(buttonKey)) searchKeys.Add(buttonKey);
            // Fallback-имена, известные из проекта
            searchKeys.Add("DeskButton");
            searchKeys.Add("ShowDirectorDeskButton");
            searchKeys.Add("DirectorDeskButton");

            // Если задан ключ кнопки — ждём клика ТОЛЬКО по ней через Button.onClick
            Button targetButton = null;
            string resolvedKey = null;

            if (SceneObjectRegistry.Instance != null)
            {
                foreach (var key in searchKeys)
                {
                    targetButton = SceneObjectRegistry.Instance.GetComponent<Button>(key);
                    if (targetButton != null) { resolvedKey = key; break; }
                }
            }
            if (targetButton == null)
            {
                foreach (var key in searchKeys)
                {
                    var go = GameObject.Find(key);
                    if (go != null)
                    {
                        targetButton = go.GetComponent<Button>();
                        if (targetButton != null) { resolvedKey = key; break; }
                    }
                }
            }
            // Рекурсивный поиск по сцене
            if (targetButton == null)
            {
                var buttons = Object.FindObjectsOfType<Button>(true);
                foreach (var key in searchKeys)
                {
                    foreach (var btn in buttons)
                    {
                        if (btn.gameObject.name == key || btn.gameObject.name.Contains(key))
                        {
                            targetButton = btn;
                            resolvedKey = key;
                            break;
                        }
                    }
                    if (targetButton != null) break;
                }
            }

            if (targetButton != null)
            {
                Debug.Log($"[ShowArrowNode] Waiting for click on Button '{resolvedKey}' (gameObject='{targetButton.gameObject.name}')");
                bool clicked = false;
                UnityEngine.Events.UnityAction call = () => clicked = true;
                targetButton.onClick.AddListener(call);

                float timer = 0f;
                while (!clicked)
                {
                    if (timeout > 0)
                    {
                        timer += Time.unscaledDeltaTime;
                        if (timer >= timeout)
                        {
                            Debug.Log($"[ShowArrowNode] Таймаут {timeout}с ожидания клика по '{resolvedKey}'");
                            break;
                        }
                    }
                    yield return null;
                }

                targetButton.onClick.RemoveListener(call);
                Debug.Log($"[ShowArrowNode] Button click received or timeout");
                yield break;
            }

            // Если buttonKey был задан, но кнопка не найдена — это ошибка
            if (!string.IsNullOrEmpty(buttonKey))
            {
                Debug.LogWarning($"[ShowArrowNode] Кнопка '{buttonKey}' не найдена — ожидаю любой клик мыши (fallback)");
            }

            // Fallback: любой клик мыши
            float t = 0f;
            while (!Input.GetMouseButtonDown(0))
            {
                if (timeout > 0)
                {
                    t += Time.unscaledDeltaTime;
                    if (t >= timeout) break;
                }
                yield return null;
            }
        }
    }
}
