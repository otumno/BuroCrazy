// Файл: StaffScheduleEditor.cs (должен лежать в папке "Editor")
using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(StaffController), true)] // true означает, что редактор будет работать и для всех наследников
public class StaffScheduleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Сначала рисуем все стандартные поля (homePoint, kitchenPoint и т.д.)
        DrawDefaultInspector();

        // Получаем наш StaffController
        StaffController staff = (StaffController)target;

        // Ищем на сцене ClientSpawner
        ClientSpawner spawner = FindFirstObjectByType<ClientSpawner>();

        if (spawner == null || spawner.periods == null || spawner.periods.Length == 0)
        {
            EditorGUILayout.HelpBox("ClientSpawner не найден на сцене или у него не настроены периоды.", MessageType.Warning);
            return;
        }

        // Рисуем красивый заголовок
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Рабочие Периоды (График)", EditorStyles.boldLabel);

        // Проходим по всем периодам, которые есть в ClientSpawner
        foreach (var period in spawner.periods)
        {
            if (string.IsNullOrEmpty(period.periodName)) continue;

            // Проверяем, есть ли этот период в списке рабочих периодов у сотрудника
            bool isWorkingInThisPeriod = staff.workPeriods.Contains(period.periodName);

            // Рисуем галочку (Toggle)
            bool shouldWork = EditorGUILayout.Toggle(period.periodName, isWorkingInThisPeriod);

            // Если состояние галочки изменилось
            if (shouldWork != isWorkingInThisPeriod)
            {
                if (shouldWork)
                {
                    // Если галочку поставили - добавляем период в список
                    if (!staff.workPeriods.Contains(period.periodName))
                    {
                        staff.workPeriods.Add(period.periodName);
                    }
                }
                else
                {
                    // Если галочку убрали - удаляем период из списка
                    staff.workPeriods.Remove(period.periodName);
                }
                // Помечаем объект как "измененный", чтобы Unity сохранил изменения
                EditorUtility.SetDirty(staff);
            }
        }
    }
}