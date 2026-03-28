using UnityEngine;
using UnityEditor;
using System.IO;

public class DataResetTool
{
    // ВОТ ЭТА СТРОКА ДОБАВЛЯЕТ КНОПКУ:
	
	[MenuItem("Tools/Reset User Data")]
   
    public static void ResetData()
    {
        if (EditorUtility.DisplayDialog("Сброс данных", 
            "Вы уверены, что хотите удалить ВСЕ сохранения, ачивки и настройки туториала? Это действие необратимо.", "Да", "Отмена"))
        {
            // 1. Очищаем PlayerPrefs
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // 2. Удаляем файлы из persistentDataPath
            if (Directory.Exists(Application.persistentDataPath))
            {
                DirectoryInfo di = new DirectoryInfo(Application.persistentDataPath);
                foreach (FileInfo file in di.GetFiles()) file.Delete();
                foreach (DirectoryInfo dir in di.GetDirectories()) dir.Delete(true);
            }

            Debug.Log("<color=green></color> Все данные успешно удалены. Следующий запуск будет как первый!");
        }
    }
}