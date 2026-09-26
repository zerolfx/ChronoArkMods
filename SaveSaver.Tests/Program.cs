using System;
using System.Linq;
using GameDataEditor;
using HarmonyLib;
using SaveSaver;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static partial class Program
{
    private static int passed;

    private static int Main()
    {
        var plugin = new SaveSaverPlugin();
        try
        {
            plugin.Initialize();
            Run("Harmony registers all nine checkpoint hooks", () =>
            {
                var targets = new[]
                {
                    AccessTools.Method(typeof(FieldSystem), nameof(FieldSystem.BattleStart)),
                    AccessTools.Method(typeof(FieldSystem), nameof(FieldSystem.BattleStart_MapEnemy)),
                    AccessTools.Method(typeof(SaveManager), nameof(SaveManager.QuitSave)),
                    AccessTools.Method(typeof(SaveManager), nameof(SaveManager.ProgressOneSave)),
                    AccessTools.Method(typeof(RE_TrialofStrength), nameof(RE_TrialofStrength.UseButton1)),
                    AccessTools.Method(typeof(RE_ShiranuiEvent), nameof(RE_ShiranuiEvent.UseButton1)),
                    AccessTools.Method(typeof(RE_GrandCasino), nameof(RE_GrandCasino.UseButton2)),
                    AccessTools.Method(typeof(BloodyMist), nameof(BloodyMist.DoubleBattle)),
                    AccessTools.Method(typeof(PlayData), nameof(PlayData.GameEndInit))
                };
                foreach (var target in targets)
                    Require(Harmony.GetPatchInfo(target)?.Owners.Contains(plugin.GetGuid()) == true,
                        "Missing patch: " + target.Name);
            });

            Run("Direct battle captures the encounter before it is consumed", () =>
                AssertCheckpointThenTransition(() => StartBattle()));
            Run("Map enemy battle captures the encounter before it is consumed", () =>
                AssertCheckpointThenTransition(() => FieldSystem.instance.BattleStart_MapEnemy()));

            Run("Repeated battle requests cannot replace the checkpoint", () =>
            {
                StartBattle();
                PlayData.EncounterPresent = false;
                FieldSystem.instance.BattleStart_MapEnemy();
                Require(SaveManager.savemanager.Checkpoints == 1 && SaveManager.savemanager.SavedEncounter,
                    "Repeated request overwrote the encounter checkpoint");
                Require(FieldSystem.instance.BattleRequests == 2 && FieldSystem.instance.BattlesStarted == 1,
                    "Original battle request behavior changed");
            });

            Run("Story battles leave the checkpoint unchanged", () =>
            {
                PlayData.StoryPart = true;
                AssertBattleWithoutCheckpoint();
            });
            Run("Battles outside a run leave the checkpoint unchanged", () =>
            {
                PlayData.GameStarted = false;
                AssertBattleWithoutCheckpoint();
            });
            Run("Requests in Battle do not save battle state", () =>
            {
                SceneManager.ActiveScene = "Battle";
                AssertBattleWithoutCheckpoint();
            });
            Run("Missing run data does not save or block battle", () =>
            {
                PlayData.TSavedata = null;
                AssertBattleWithoutCheckpoint();
            });
            Run("Missing save manager does not block battle", () =>
            {
                SaveManager.savemanager = null;
                StartBattle();
                Require(FieldSystem.instance.BattlesStarted == 1, "Battle was blocked");
            });
            Run("Save errors are reported without blocking battle", () =>
            {
                SaveManager.savemanager.ThrowOnSave = true;
                StartBattle();
                Require(SaveManager.savemanager.SaveAttempts == 1, "Save was not attempted");
                Require(FieldSystem.instance.BattlesStarted == 1, "Save failure blocked battle");
                Require(Debug.Errors.Any(e => e.Contains("Simulated save failure")),
                    "Save failure was not reported");
            });

            Run("Quitting during Field-to-Battle transition preserves the encounter", () =>
            {
                StartBattle();
                PlayData.EncounterPresent = false;
                SaveManager.QuitSave();
                Require(SaveManager.savemanager.Checkpoints == 1 && SaveManager.savemanager.SavedEncounter,
                    "Transition exit overwrote the encounter checkpoint");
                Require(SaveManager.savemanager.QuitCalls == 0, "Unsafe original quit logic ran");
            });
            Run("Quitting Battle after its singleton is destroyed preserves the checkpoint", () =>
            {
                SceneManager.ActiveScene = "Battle";
                BattleSystem.instance = null;
                FieldSystem.instance = null;
                SaveManager.QuitSave();
                Require(SaveManager.savemanager.QuitCalls == 0 && SaveManager.savemanager.DeletedSaves == 0,
                    "Battle teardown deleted the checkpoint");
            });
            Run("Quitting an active battle preserves the checkpoint", () =>
            {
                SceneManager.ActiveScene = "Battle";
                BattleSystem.instance = new BattleSystem();
                SaveManager.QuitSave();
                Require(SaveManager.savemanager.QuitCalls == 0, "Battle quit protection did not run");
            });
            Run("Normal Field exit still saves current progress", () =>
            {
                SaveManager.QuitSave();
                Require(SaveManager.savemanager.QuitCalls == 1 && SaveManager.savemanager.Checkpoints == 1,
                    "Normal field save was suppressed");
            });
            Run("Other scenes keep the original quit cleanup", () =>
            {
                SceneManager.ActiveScene = "Other";
                FieldSystem.instance = null;
                SaveManager.QuitSave();
                Require(SaveManager.savemanager.QuitCalls == 1 &&
                        SaveManager.savemanager.DeletedSaves == 1 &&
                        SaveManager.savemanager.PermanentSaves == 1,
                    "Original non-battle quit cleanup was suppressed");
            });
            Run("Main menu keeps the original early return", () =>
            {
                SceneManager.ActiveScene = "Main";
                SaveManager.QuitSave();
                Require(SaveManager.savemanager.QuitCalls == 1 &&
                        SaveManager.savemanager.DeletedSaves == 0 &&
                        SaveManager.savemanager.Checkpoints == 0, "Main menu behavior changed");
            });

            RunEncounterChecks();

            Run("Dispose clears a pending event checkpoint", () =>
            {
                new RE_ShiranuiEvent().UseButton1();
                plugin.Dispose();
                SaveManager.savemanager.ProgressOneSave();
                Require(SaveManager.savemanager.Checkpoints == 2, "Dispose left a pending save restriction");
            });
            Run("Dispose removes every patch and restores original behavior", () =>
            {
                Require(!Harmony.GetAllPatchedMethods().Any(m =>
                    Harmony.GetPatchInfo(m).Owners.Contains(plugin.GetGuid())), "Patches survived Dispose");
                StartBattle();
                Require(SaveManager.savemanager.Checkpoints == 0, "Battle hook survived Dispose");
                SaveManager.QuitSave();
                Require(SaveManager.savemanager.QuitCalls == 1 && SaveManager.savemanager.Checkpoints == 1,
                    "Quit hook survived Dispose");
            });
            Console.WriteLine($"All {passed} SaveSaver regression checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            plugin.Dispose();
        }
    }

    private static void AssertCheckpointThenTransition(Action startBattle)
    {
        startBattle();
        PlayData.EncounterPresent = false;
        Require(SaveManager.savemanager.Checkpoints == 1 && SaveManager.savemanager.SavedEncounter,
            "Encounter was not captured before battle");
        Require(FieldSystem.instance.BattlesStarted == 1, "Battle did not start");
    }

    private static void StartBattle(string queue = "Normal") =>
        FieldSystem.instance.BattleStart(new GDEEnemyQueueData(queue));

    private static void AssertBattleWithoutCheckpoint()
    {
        StartBattle();
        Require(SaveManager.savemanager.Checkpoints == 0, "Unsafe checkpoint was written");
        Require(FieldSystem.instance.BattlesStarted == 1, "Original battle was blocked");
    }

    private static void Run(string name, Action test)
    {
        SaveManager.savemanager = new SaveManager();
        FieldSystem.instance = new FieldSystem();
        BattleSystem.instance = null;
        PlayData.GameStarted = true;
        PlayData.StoryPart = false;
        PlayData.TSavedata = new TempSaveData();
        PlayData.EncounterPresent = true;
        PlayData.Gold = 1000;
        SceneManager.ActiveScene = "Field";
        Debug.Errors.Clear();
        try
        {
            test();
        }
        catch (Exception exception)
        {
            throw new Exception("FAIL: " + name, exception);
        }
        passed++;
        Console.WriteLine("PASS: " + name);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
