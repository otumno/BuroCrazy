using System;
using Gameplay;
using Managers;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WalletSaveCheck
{
    public static string Run()
    {
        if (Application.isPlaying || FinancialLedgerManager.Instance != null)
            throw new InvalidOperationException("Run outside gameplay with no active ledger.");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("WalletSaveCheck");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var wallet = root.AddComponent<PlayerWallet>();
            var stack = root.AddComponent<EnvelopeStack>();
            stack.maxCapacity = 1;
            wallet.SetMoney(250);
            Require(stack.TryPrepareEnvelope(wallet, "Payroll"), "First envelope rejected.");
            Require(!stack.TryPrepareEnvelope(wallet, "Payroll"), "Full safe accepted another envelope.");
            var data = SaveData.CreateEmpty();
            data.money = wallet.GetCurrentMoney();
            data.salaryEnvelopeCount = stack.CurrentEnvelopeCount;
            data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            wallet.ResetState();
            stack.RestoreCount(0);
            wallet.SetMoney(data.money);
            stack.RestoreCount(data.salaryEnvelopeCount);
            Require(wallet.GetCurrentMoney() == 150 && stack.CurrentEnvelopeCount == 1, "Round trip lost funds or envelopes.");
            var legacy = JsonUtility.FromJson<SaveData>("{\"money\":50}");
            wallet.SetMoney(legacy.money);
            stack.RestoreCount(legacy.salaryEnvelopeCount);
            Require(!stack.TryPrepareEnvelope(wallet, "Payroll"), "Underfunded envelope was created.");
            Require(wallet.GetCurrentMoney() == 50 && stack.CurrentEnvelopeCount == 0, "Legacy wallet or safe changed.");
            return "PASS: one charge per envelope, capacity and funds, JSON round trip and legacy save.";
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
