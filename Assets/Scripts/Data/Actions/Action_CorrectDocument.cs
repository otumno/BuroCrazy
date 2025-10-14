// Файл: Assets/Scripts/Data/Actions/Action_CorrectDocument.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Action_CorrectDocument", menuName = "Bureau/Actions/CorrectDocument")]
public class CorrectDocumentAction : StaffAction
{
    public CorrectDocumentAction()
    {
        category = ActionCategory.System;
    }

    // Это действие теперь является "навыком", оно не может быть выбрано напрямую.
    public override bool AreConditionsMet(StaffController staff)
    {
        return false;
    }

    public override System.Type GetExecutorType()
    {
        return null; // У него нет своего исполнителя
    }
}