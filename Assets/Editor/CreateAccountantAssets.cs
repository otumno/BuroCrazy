// Assets/Editor/CreateAccountantAssets.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateAccountantAssets
{
    private const string ActionsFolder = "Assets/Thoughts/StaffActions/TacticalAction";
    private const string RanksFolder = "Assets/Thoughts/Ranks";

    [MenuItem("Bureau/Create Accountant Assets")]
    public static void CreateAll()
    {
        EnsureFolder(ActionsFolder);
        EnsureFolder(RanksFolder);

        // Шаг 1: создать Action-ассеты (AccountantMoneyGen, AccountantCoverSchemes)
        var moneyGenAction = CreateOrLoad<AccountantMoneyGenAction>($"{ActionsFolder}/Action_AccountantMoneyGen.asset");
        var coverSchemesAction = CreateOrLoad<AccountantCoverSchemesAction>($"{ActionsFolder}/Action_AccountantCoverSchemes.asset");
        var doBookkeeping = AssetDatabase.LoadAssetAtPath<DoBookkeepingAction>($"{ActionsFolder}/Action_DoBookkeeping.asset");
        var prepareSalaries = AssetDatabase.LoadAssetAtPath<PrepareSalariesAction>($"{ActionsFolder}/Action_PrepareSalaries.asset");

        // Шаг 2: бэкап старого Rank_1_Accountant.asset
        string oldRankPath = $"{RanksFolder}/Rank_1_Accountant.asset";
        if (File.Exists(oldRankPath))
        {
            string backupPath = $"{RanksFolder}/Rank_1_Accountant_OLD_BACKUP.asset";
            if (!File.Exists(backupPath))
            {
                AssetDatabase.CopyAsset(oldRankPath, backupPath);
                Debug.Log($"[CreateAccountantAssets] Бэкап: {backupPath}");
            }
        }

        // Шаг 3: создать 3 ранговых файла Accountant по образцу Cashier
        var unlockedActions = new StaffAction[] { doBookkeeping, moneyGenAction, coverSchemesAction };

        var rank1 = CreateOrLoadRank($"{RanksFolder}/Rank_1_Accountant_1.asset",
            0, "Бухгалтер", 1000, 300, 1.4f, 1, 4,
            unlockedActions);

        var rank2 = CreateOrLoadRank($"{RanksFolder}/Rank_1_Accountant_2.asset",
            1, "Бухгалтер", 2000, 400, 1.5f, 2, 4,
            unlockedActions);

        var rank3 = CreateOrLoadRank($"{RanksFolder}/Rank_1_Accountant_3.asset",
            2, "Бухгалтер", 4000, 300, 1.7f, 3, 5,
            unlockedActions);

        // Шаг 4: связать possiblePromotions
        if (rank1 != null) rank1.possiblePromotions = new List<RankData> { rank2 };
        if (rank2 != null) rank2.possiblePromotions = new List<RankData> { rank3 };
        if (rank3 != null) rank3.possiblePromotions = new List<RankData>();

        EditorUtility.SetDirty(rank1);
        EditorUtility.SetDirty(rank2);
        EditorUtility.SetDirty(rank3);

        // Шаг 5: обновить Rank_1_Cashier_*.asset — удалить PrepareSalaries из unlockedActions
        UpdateCashierRanks(prepareSalaries);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CreateAccountantAssets] Готово. Проверьте RankDatabase.asset — обновите ссылки на новые Rank_1_Accountant_1/2/3.asset.");
    }

    private static T CreateOrLoad<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static RankData CreateOrLoadRank(string path,
        int level, string name, int expRequired, int promoCost, float salaryMult,
        int maxActions, int workPeriods,
        StaffAction[] unlocked)
    {
        var existing = AssetDatabase.LoadAssetAtPath<RankData>(path);
        if (existing != null) return existing;

        var rank = ScriptableObject.CreateInstance<RankData>();
        rank.rankLevel = level;
        rank.rankName = name;
        rank.experienceRequired = expRequired;
        rank.promotionCost = promoCost;
        rank.salaryMultiplier = salaryMult;
        rank.maxActions = maxActions;
        rank.workPeriodsCount = workPeriods;
        rank.associatedRole = StaffController.Role.Accountant;

        var actions = new List<StaffAction>();
        if (unlocked != null)
        {
            foreach (var a in unlocked)
            {
                if (a != null) actions.Add(a);
            }
        }
        rank.unlockedActions = actions;
        rank.possiblePromotions = new List<RankData>();

        AssetDatabase.CreateAsset(rank, path);
        EditorUtility.SetDirty(rank);
        return rank;
    }

    private static void UpdateCashierRanks(PrepareSalariesAction prepareSalaries)
    {
        if (prepareSalaries == null) return;
        string[] paths = {
            $"{RanksFolder}/Rank_1_Cashier_1.asset",
            $"{RanksFolder}/Rank_1_Cashier_2.asset",
            $"{RanksFolder}/Rank_1_Cashier_3.asset"
        };
        foreach (var p in paths)
        {
            var rank = AssetDatabase.LoadAssetAtPath<RankData>(p);
            if (rank == null) continue;
            if (rank.unlockedActions == null) continue;
            int removed = rank.unlockedActions.RemoveAll(a => a == prepareSalaries);
            if (removed > 0)
            {
                EditorUtility.SetDirty(rank);
                Debug.Log($"[CreateAccountantAssets] {p}: удалено {removed} ссылок на PrepareSalaries.");
            }
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
        string leaf = Path.GetFileName(folder);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
