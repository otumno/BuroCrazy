using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CreateRoleColorDatabase : EditorWindow
{
    [MenuItem("Bureau/Create Role Color Database")]
    public static void Create()
    {
        var db = ScriptableObject.CreateInstance<RoleColorDatabase>();
        db.roleColors = new List<RoleColorEntry>
        {
            new RoleColorEntry { role = StaffController.Role.Intern, color = new Color(0.6f, 0.8f, 0.4f) },
            new RoleColorEntry { role = StaffController.Role.Clerk, color = new Color(0.4f, 0.6f, 0.8f) },
            new RoleColorEntry { role = StaffController.Role.Registrar, color = new Color(0.7f, 0.4f, 0.8f) },
            new RoleColorEntry { role = StaffController.Role.Cashier, color = new Color(0.9f, 0.7f, 0.2f) },
            new RoleColorEntry { role = StaffController.Role.Archivist, color = new Color(0.6f, 0.5f, 0.4f) },
            new RoleColorEntry { role = StaffController.Role.Guard, color = new Color(0.3f, 0.3f, 0.5f) },
            new RoleColorEntry { role = StaffController.Role.Janitor, color = new Color(0.5f, 0.5f, 0.5f) },
            new RoleColorEntry { role = StaffController.Role.OfficeManager, color = new Color(0.9f, 0.4f, 0.4f) },
            new RoleColorEntry { role = StaffController.Role.Accountant, color = new Color(0.2f, 0.8f, 0.3f) },
            new RoleColorEntry { role = StaffController.Role.ServiceWorker, color = new Color(0.8f, 0.5f, 0.3f) },
        };

        AssetDatabase.CreateAsset(db, "Assets/Data/RoleColorDatabase.asset");
        AssetDatabase.SaveAssets();
        Debug.Log($"[RoleColorDatabase] Создан: Assets/Data/RoleColorDatabase.asset");
    }
}
