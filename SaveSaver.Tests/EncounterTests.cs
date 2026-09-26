using System;
using GameDataEditor;
using UnityEngine.SceneManagement;

internal static partial class Program
{
    private static void RunEncounterChecks()
    {
        Run("Loading a run does not overwrite its checkpoint", () =>
        {
            PlayData.TSavedata.IsLoaded = true;
            AssertBattleWithoutCheckpoint();
        });
        Run("A surviving Battle singleton protects quitting after a scene switch", () =>
        {
            BattleSystem.instance = new BattleSystem();
            SaveManager.QuitSave();
            Require(SaveManager.savemanager.SaveAttempts == 0 && SaveManager.savemanager.QuitCalls == 0,
                "Battle teardown allowed an unsafe quit save");
        });
        Run("Explicit game recovery saves remain available in the Battle scene", () =>
        {
            SceneManager.ActiveScene = "Battle";
            BattleSystem.instance = null;
            SaveManager.savemanager.ProgressOneSave();
            Require(SaveManager.savemanager.Checkpoints == 1,
                "Battle scene suppressed the game's Hope Mode recovery save");
        });
        Run("Ending a run permits recovery saves despite a stale transition flag", () =>
        {
            new RE_ShiranuiEvent().UseButton1();
            FieldSystem.instance.BattleStartFlag = true;
            SceneManager.ActiveScene = "Battle";
            PlayData.GameEndInit();
            SaveManager.savemanager.ProgressOneSave();
            Require(SaveManager.savemanager.Checkpoints == 2, "Ended run retained unsafe-save restrictions");
        });
        Run("Trial checkpoint precedes the entry fee and event consumption", () =>
        {
            new RE_TrialofStrength().UseButton1();
            Require(SaveManager.savemanager.Checkpoints == 1 && SaveManager.savemanager.SavedGold == 1000 &&
                    !SaveManager.savemanager.SavedEventUsed, "Trial checkpoint lost the fee or event");
            Require(PlayData.Gold == 500 && PlayData.TSavedata.EventUsed && FieldSystem.instance.BattlesStarted == 1,
                "Trial gameplay changed");
        });
        Run("Unaffordable trial does not create a checkpoint", () =>
        {
            PlayData.Gold = 499;
            new RE_TrialofStrength().UseButton1();
            Require(SaveManager.savemanager.Checkpoints == 0 && FieldSystem.instance.BattlesStarted == 0,
                "Unaffordable trial saved or started combat");
        });
        Run("Trial exceptions release nested-save suppression", () =>
        {
            ExpectFailure(() => new RE_TrialofStrength { ThrowBeforeBattle = true }.UseButton1());
            StartBattle();
            Require(SaveManager.savemanager.Checkpoints == 2, "Trial exception disabled subsequent checkpoints");
        });
        Run("DorchiX checkpoint restores the consumed boss selector only on disk", () =>
        {
            StartBattle(GDEItemKeys.EnemyQueue_Queue_DorchiX);
            Require(SaveManager.savemanager.SavedSwordSanctuary, "Reload would select the wrong boss");
            Require(!PlayData.TSavedata.SwordSanctuary, "Live boss selector was changed");
        });
        Run("DorchiX save failure still restores the live boss selector", () =>
        {
            SaveManager.savemanager.ThrowOnSave = true;
            StartBattle(GDEItemKeys.EnemyQueue_Queue_DorchiX);
            Require(!PlayData.TSavedata.SwordSanctuary && FieldSystem.instance.BattlesStarted == 1,
                "Failed save leaked a boss flag or blocked combat");
        });
        Run("Shiranui checkpoint survives coroutine delay and nested battle entry", () =>
        {
            var encounter = new RE_ShiranuiEvent();
            encounter.UseButton1();
            AssertPendingCheckpoint();
            encounter.FinishDelay();
            Require(SaveManager.savemanager.Checkpoints == 1 && !SaveManager.savemanager.SavedEventUsed,
                "Delayed Shiranui battle saved the consumed event");
            FinishBattle();
            SaveManager.savemanager.ProgressOneSave();
            Require(SaveManager.savemanager.Checkpoints == 2, "Completed event still prevented saving");
        });
        Run("Casino checkpoint precedes the reward and survives its delay", () =>
        {
            var encounter = new RE_GrandCasino();
            encounter.UseButton2();
            AssertPendingCheckpoint();
            Require(PlayData.Gold == 1500 && SaveManager.savemanager.SavedGold == 1000,
                "Casino checkpoint duplicated the pre-battle reward");
            encounter.FinishDelay();
            Require(SaveManager.savemanager.Checkpoints == 1 && !SaveManager.savemanager.SavedEventUsed,
                "Delayed casino entry replaced the recoverable event");
        });
        Run("Delayed-event exceptions release pending checkpoint protection", () =>
        {
            ExpectFailure(() => new RE_ShiranuiEvent { ThrowBeforeDelay = true }.UseButton1());
            SaveManager.savemanager.ProgressOneSave();
            Require(SaveManager.savemanager.Checkpoints == 2, "Event exception permanently blocked saves");
        });
        Run("Failed event checkpoint does not create a pending save restriction", () =>
        {
            SaveManager.savemanager.ThrowOnSave = true;
            new RE_ShiranuiEvent().UseButton1();
            SaveManager.savemanager.ThrowOnSave = false;
            SaveManager.savemanager.ProgressOneSave();
            Require(SaveManager.savemanager.Checkpoints == 1, "Failed event checkpoint blocked later saves");
        });
        Run("A different battle queue replaces a stale pending checkpoint", () =>
        {
            new RE_ShiranuiEvent().UseButton1();
            StartBattle("OtherEncounter");
            Require(SaveManager.savemanager.Checkpoints == 2, "Unrelated encounter reused a stale checkpoint");
        });
        Run("A map enemy replaces a stale pending checkpoint", () =>
        {
            new RE_ShiranuiEvent().UseButton1();
            FieldSystem.instance.BattleStart_MapEnemy();
            Require(SaveManager.savemanager.Checkpoints == 2, "Map enemy reused a stale event checkpoint");
        });

        AssertPendingExpires("Replacing FieldSystem", () => FieldSystem.instance = new FieldSystem());
        AssertPendingExpires("Replacing the run", () => PlayData.TSavedata = new TempSaveData());
        AssertPendingExpires("Changing stage within the run", () => PlayData.TSavedata.StageNum++);
        AssertPendingExpires("Changing map within the run", () => PlayData.TSavedata.NowStageMapKey = "NextMap");
        AssertPendingExpires("Ending the run", () =>
        {
            PlayData.GameEndInit();
            // Keeping the same objects here proves the end-run hook clears pending state.
            PlayData.GameStarted = true;
        });

        Run("Chained boss entry and the first battle's late autosave preserve the first checkpoint", () =>
        {
            StartBattle("FirstBoss");
            FinishBattle();
            PlayData.EncounterPresent = false;
            new BloodyMist().DoubleBattle();
            SaveManager.savemanager.ProgressOneSave();
            Require(SaveManager.savemanager.Checkpoints == 1 && SaveManager.savemanager.SavedEncounter,
                "Chained battle replaced the first encounter checkpoint");
            FinishBattle();
            SaveManager.savemanager.ProgressOneSave();
            Require(SaveManager.savemanager.Checkpoints == 2, "Finished chained battle prevented later saves");
        });
        Run("Chained battle exceptions release nested-save suppression", () =>
        {
            ExpectFailure(() => new BloodyMist { ThrowBeforeBattle = true }.DoubleBattle());
            StartBattle();
            Require(SaveManager.savemanager.Checkpoints == 1, "Chained battle exception disabled future checkpoints");
        });
    }

    private static void AssertPendingCheckpoint()
    {
        Require(SaveManager.savemanager.Checkpoints == 1, "Event did not create its initial checkpoint");
        SaveManager.savemanager.ProgressOneSave();
        SaveManager.QuitSave();
        Require(SaveManager.savemanager.Checkpoints == 1 && SaveManager.savemanager.QuitCalls == 0,
            "Coroutine delay allowed an unsafe save");
    }

    private static void AssertPendingExpires(string change, Action mutate)
    {
        Run(change + " invalidates a pending encounter", () =>
        {
            new RE_ShiranuiEvent().UseButton1();
            mutate();
            SaveManager.savemanager.ProgressOneSave();
            Require(SaveManager.savemanager.Checkpoints == 2, change + " left a stale save restriction");
        });
    }

    private static void FinishBattle()
    {
        FieldSystem.instance.BattleStartFlag = false;
        BattleSystem.instance = null;
        SceneManager.ActiveScene = "Field";
    }

    private static void ExpectFailure(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new Exception("Expected the original method's failure to propagate");
    }
}
