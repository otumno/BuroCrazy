using UnityEngine;
using System.Collections;
using CinematicSystem;
using UnityEngine.SceneManagement;

namespace CinematicSystem.Nodes
{
    [CreateAssetMenu(menuName = "Bureau/Cinematic/Nodes/Show Arrow")]
    public class ShowArrowNode : NextNode
    {
        public override string GetNodeType() => "show_arrow";
        
        public override IEnumerator Execute(CinematicPlayer player)
        {
            // Показать стрелку - ищем 4 способами как в FirstDayTutorial
            GameObject hintArrow = FindHintArrow();
            
            if (hintArrow != null)
            {
                hintArrow.SetActive(true);
                Debug.Log("[ShowArrowNode] Arrow shown");
            }
            else
            {
                Debug.LogWarning("[ShowArrowNode] HintArrow not found!");
            }
            
            // Ожидаем клик
            yield return new WaitUntil(() => Input.GetMouseButtonDown(0));
            
            // Скрываем стрелку после клика
            if (hintArrow != null)
            {
                hintArrow.SetActive(false);
            }
            
            player.GoToNextNode(nextNode);
        }
        
        private GameObject FindHintArrow()
        {
            // Способ 1: По тегу
            GameObject arrowByTag = GameObject.FindWithTag("HintArrow");
            if (arrowByTag != null) return arrowByTag;
            
            // Способ 2: По имени
            GameObject[] allObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject obj in allObjects)
            {
                if (obj.name.Contains("HintArrow"))
                    return obj;
                    
                Transform child = obj.transform.Find("HintArrow");
                if (child != null)
                    return child.gameObject;
            }
            
            // Способ 3: В StartOfDayPanel
            var startOfDay = Object.FindObjectOfType<StartOfDayPanel>();
            if (startOfDay != null)
            {
                var field = startOfDay.GetType().GetField("hintArrow", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    return field.GetValue(startOfDay) as GameObject;
                }
            }
            
            return null;
        }
    }
}