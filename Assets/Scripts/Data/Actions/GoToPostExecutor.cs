// Assets/Scripts/Data/Actions/GoToPostExecutor.cs
using UnityEngine;
using System.Collections;
using Managers;

public class GoToPostExecutor : ActionExecutor
{
    public override bool IsInterruptible => true;

    protected override IEnumerator ActionRoutine()
    {
        // guardPostPoint теперь ServicePoint
        var post = ScenePointsRegistry.Instance?.guardPostPoint;

        if (post != null)
        {
            // ИСПРАВЛЕНИЕ: .transform.position
            yield return staff.StartCoroutine(staff.MoveToTarget(post.transform.position, "Guarding"));
            
            while(true)
            {
                // Стоим охраняем
                yield return new WaitForSeconds(5f);
                staff.ChangeEnergy(-1);
            }
        }
        
        FinishAction(true); // Если поста нет
    }
}