// Файл: Assets/Scripts/Data/Actions/MakeArchiveRequestAction.cs
using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Action_MakeArchiveRequest", menuName = "Bureau/Actions/MakeArchiveRequest")]
public class MakeArchiveRequestAction : StaffAction
{
    public MakeArchiveRequestAction()
    {
        category = ActionCategory.System; // Делаем системным "навыком"
    }

    // Это действие не выбирается напрямую, а проверяется внутри другого Executor'а.
    public override bool AreConditionsMet(StaffController staff)
    {
        return false;
    }
    
    public override System.Type GetExecutorType()
    {
        return null;
    }
}