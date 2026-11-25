using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Managers;
using Scriptables.Progression;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilities;

// Unity CLI script run. Isolated fixtures; no Play Mode or scene/asset saves.
public static class HiringCareerAccessCheck
{
    public static string Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        var scene = EditorSceneManager.NewPreviewScene();
        var previousProgression = ProgressionManager.Instance;
        var previousHiring = HiringManager.Instance;
        var randomState = UnityEngine.Random.state;
        var instance = typeof(ProgressionManager).GetProperty("Instance");
        var createdSkills = new HashSet<CharacterSkills>();
        try
        {
            var root = new GameObject("HiringCareerAccessCheck");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var hiring = root.AddComponent<HiringManager>();
            var progression = root.AddComponent<ProgressionManager>();
            instance.SetValue(null, progression);
            HiringManager.Instance = hiring;
            progression.allJobsDatabase = Resources.LoadAll<JobTitleData>("Progression/Jobs").ToList();
            hiring.rankDatabase = UnityEditor.AssetDatabase.FindAssets("t:RankData")
                .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<RankData>(UnityEditor.AssetDatabase.GUIDToAssetPath(g))).ToList();
            hiring.allRoleData = new List<RoleData>();
            var unlocked = (HashSet<string>)typeof(ProgressionManager)
                .GetField("unlockedJobIDs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(progression);
            unlocked.Add("Job_Director");
            Require(hiring.GetMaxHireRankLevel(StaffController.Role.Intern) == 0, "Initial intern rank.");
            Require(hiring.GetMaxHireRankLevel(StaffController.Role.Registrar) == -1, "Initial specialist access leaked.");
            Require(progression.allJobsDatabase.First(j => j.jobID == "Job_Director").maxUnlockedRoleHireRankLevel == -1,
                "Unconfigured assets must retain the disabled default.");

            unlocked.Add("Job_KeeperOfSeals");
            Require(hiring.GetMaxHireRankLevel(StaffController.Role.Registrar) == 0, "Legacy role unlock lost.");
            unlocked.Add("Job_StaffOverseer");
            Require(hiring.GetMaxHireRankLevel(StaffController.Role.Registrar) == 1, "Experienced registrar unavailable.");
            Require(hiring.GetMaxHireRankLevel(StaffController.Role.Janitor) == 1, "Janitor unavailable.");
            Require(hiring.GetMaxHireRankLevel(StaffController.Role.Cashier) == -1, "Optional treasurer branch bypassed.");
            progression.allJobsDatabase.Reverse();
            Require(hiring.GetMaxHireRankLevel(StaffController.Role.Registrar) == 1, "Access depends on database order.");

            var candidate = new Candidate { Role = StaffController.Role.Registrar,
                Rank = hiring.rankDatabase.First(r => r.associatedRole == StaffController.Role.Registrar && r.rankLevel == 2) };
            Require(!hiring.CanHireAs(candidate, Enums.EmploymentType.Permanent), "Rank above access was accepted.");
            unlocked.Add("Job_DepartmentHead");
            Require(hiring.CanHireAs(candidate, Enums.EmploymentType.Permanent), "Highest unlocked rank rejected.");
            Require(hiring.GetMaxHireRankLevel(StaffController.Role.Cashier) == -1, "Higher cap opened a locked role.");
            unlocked.Add("Job_Treasurer");
            Require(hiring.GetMaxHireRankLevel(StaffController.Role.Cashier) == 2, "Later role did not inherit the cap.");
            candidate.Role = StaffController.Role.Guard;
            Require(!hiring.CanHireAs(candidate, Enums.EmploymentType.Permanent), "Mismatched role/rank accepted.");

            foreach (var job in progression.allJobsDatabase) unlocked.Add(job.jobID);
            hiring.experiencedInternChance = AnimationCurve.Constant(0, 100, 1);
            hiring.GenerateNewCandidates();
            foreach (var c in hiring.AvailableCandidates) if (c.Skills != null) createdSkills.Add(c.Skills);
            foreach (var role in hiring.rankDatabase.Select(r => r.associatedRole).Distinct())
            {
                Require(hiring.IsRoleHireable(role), "No career unlock for " + role);
                Require(hiring.AvailableCandidates.Any(c => c.Role == role), "No candidate generated for " + role);
            }
            Require(hiring.AvailableCandidates.All(c => c.Rank.rankLevel <= hiring.GetMaxHireRankLevel(c.Role)), "Generated rank exceeds access.");
            Require(hiring.AvailableCandidates.Any(c => c.Rank.rankLevel > 0), "Experienced ranks never generated.");
            return "PASS: initial interns, legacy and explicit unlocks, rank caps, locked branches, database order, candidate validation and all-role generation.";
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var skills in createdSkills) UnityEngine.Object.DestroyImmediate(skills);
            instance.SetValue(null, previousProgression);
            HiringManager.Instance = previousHiring;
            UnityEngine.Random.state = randomState;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
