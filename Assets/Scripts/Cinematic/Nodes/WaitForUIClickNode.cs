// === FILE: Assets/Scripts/Cinematic/Nodes/WaitForUIClickNode.cs ===
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CinematicSystem.Nodes
{
    /// <summary>
    /// Узел ожидания клика на UI элемент.
    /// </summary>
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Wait For UI Click")]
    public class WaitForUIClickNode : NextNode
    {
        [Tooltip("Ключ кнопки в SceneObjectRegistry")]
        public string uiElementKey;
        
        [Tooltip("Таймаут в секундах (0 = бесконечно)")]
        public float timeout = 0f;

        public override string GetNodeType() => "wait_click";

        public override IEnumerator Execute(CinematicPlayer player)
        {
            Button targetButton = null;
            if (!string.IsNullOrEmpty(uiElementKey))
            {
                targetButton = SceneObjectRegistry.Instance.GetComponent<Button>(uiElementKey);
                if (targetButton == null)
                {
                    Debug.LogError($"[WaitForUIClickNode] Кнопка не найдена: {uiElementKey}");
                    player.GoToNextNode(nextNode);
                    yield break;
                }
            }

            bool clicked = false;
            
            if (targetButton != null)
            {
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
                            Debug.Log($"[WaitForUIClickNode] Таймаут {timeout}с");
                            break;
                        }
                    }
                    yield return null;
                }
                
                targetButton.onClick.RemoveListener(call);
            }
            else
            {
                // Ждём любой клик по UI через EventSystem
                float timer = 0f;
                while (!clicked)
                {
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject() && Input.GetMouseButtonDown(0))
                    {
                        clicked = true;
                        break;
                    }
                    
                    if (timeout > 0)
                    {
                        timer += Time.unscaledDeltaTime;
                        if (timer >= timeout)
                            break;
                    }
                    yield return null;
                }
            }

            player.GoToNextNode(nextNode);
        }
    }
}