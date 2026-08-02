// Assets/Editor/ListRegionNames.cs
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Scriptables.Progression;

namespace StorySystem.EditorTools
{
    /// <summary>
    /// Одноразовый запуск: печатает все RegionData ассеты с их ID, displayName и archetype groups.
    /// Запуск: Unity -batchmode -nographics -quit -projectPath . -executeMethod StorySystem.EditorTools.ListRegionNames.Run
    /// </summary>
    public static class ListRegionNames
    {
        public static void Run()
        {
            var guids = AssetDatabase.FindAssets("t:RegionData");
            var sb = new StringBuilder();
            sb.AppendLine("=== RegionData assets ===");
            sb.AppendLine($"Найдено: {guids.Length}");
            sb.AppendLine();

            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var region = AssetDatabase.LoadAssetAtPath<RegionData>(path);
                if (region == null) continue;
                sb.AppendLine($"{region.name} | id={region.regionID} | name={region.displayName}");
                sb.AppendLine($"  path: {path}");
                sb.AppendLine($"  description: {(string.IsNullOrEmpty(region.description) ? "<empty>" : region.description.Replace("\n", " "))}");
                sb.AppendLine($"  unlock: influence={region.unlockCostInfluence}, money={region.unlockCostMoney}");
                sb.AppendLine($"  flow: max={region.maxDailyFlow}, variance={region.flowVariance:P0}, ramp={region.rampUpDays}d");
                if (region.archetypeGroups != null && region.archetypeGroups.Count > 0)
                {
                    sb.AppendLine($"  archetypeGroups: {string.Join(", ", region.archetypeGroups)}");
                }
                if (region.groupWeights != null && region.groupWeights.Count > 0)
                {
                    sb.AppendLine($"  groupWeights:");
                    foreach (var w in region.groupWeights)
                    {
                        sb.AppendLine($"    - {w.groupID}: {w.weight}");
                    }
                }
                sb.AppendLine();
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outPath = Path.Combine(projectRoot, "regions_list.txt");
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }
    }
}