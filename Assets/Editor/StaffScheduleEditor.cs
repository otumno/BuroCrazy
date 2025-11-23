// Файл: StaffScheduleEditor.cs (должен лежать в папке "Editor")
using Scriptables;
using UnityEditor;

namespace Editor
{
    [CustomEditor(typeof(StaffController), editorForChildClasses: true)] // true означает, что редактор будет работать и для всех наследников
    public class StaffScheduleEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Сначала рисуем все стандартные поля (homePoint, kitchenPoint и т.д.)
            DrawDefaultInspector();

            // Получаем наш StaffController
            StaffController staff = (StaffController)target;

            // Ищем на сцене ClientSpawner
            var spawner = FindFirstObjectByType<CalendarDayEditor>();

            if (spawner == null || spawner.Periods == null || spawner.Periods.Length == 0)
            {
                EditorGUILayout.HelpBox("ClientSpawner не найден на сцене или у него не настроены периоды.", MessageType.Warning);
                return;
            }

            // Рисуем красивый заголовок
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Рабочие Периоды (График)", EditorStyles.boldLabel);

            // Проходим по всем периодам, которые есть в ClientSpawner
            foreach (var period in spawner.Periods)
            {
                // Проверяем, есть ли этот период в списке рабочих периодов у сотрудника
                bool isWorkingInThisPeriod = staff.WorkingPeriods.Contains(period.PeriodType);

                // Рисуем галочку (Toggle)
                var shouldWork = EditorGUILayout.Toggle(period.PeriodType.ToString(), isWorkingInThisPeriod);

                // Если состояние галочки изменилось
                if (shouldWork != isWorkingInThisPeriod)
                {
                    if (shouldWork)
                    {
                        // Если галочку поставили - добавляем период в список
                        if (!staff.WorkingPeriods.Contains(period.PeriodType))
                        {
                            staff.WorkingPeriods.Add(period.PeriodType);
                        }
                    }
                    else
                    {
                        // Если галочку убрали - удаляем период из списка
                        staff.WorkingPeriods.Remove(period.PeriodType);
                    }
                    // Помечаем объект как "измененный", чтобы Unity сохранил изменения
                    EditorUtility.SetDirty(staff);
                }
            }
        }
    }
}