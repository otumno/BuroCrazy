using UnityEngine;

[CreateAssetMenu(fileName = "Action_CheckDocument", menuName = "Bureau/Actions/CheckDocument")]
public class CheckDocumentAction : StaffAction
{
    // Это действие теперь является "навыком", а не самостоятельной задачей.
    // "Мозг" ИИ никогда не должен выбирать его напрямую.
    public override bool AreConditionsMet(StaffController staff)
    {
        return false; 
    }

    public override System.Type GetExecutorType()
    {
        return null; // У него нет своего исполнителя
    }
}