using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GameDataEditor;

// This executable exercises the real Harmony patches without loading Unity or
// accessing the game, its installation directory, or any player save files.
namespace ChronoArkMod.Plugin
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PluginConfig : Attribute
    {
        public PluginConfig(string name, string author, string version) { }
    }

    public abstract class ChronoArkPlugin
    {
        public abstract void Initialize();
        public abstract void Dispose();
        public string GetGuid() => "SaveSaver.RegressionTests";
    }
}

namespace UnityEngine
{
    public static class Debug
    {
        public static readonly List<string> Errors = new List<string>();
        public static void Log(object message) { }
        public static void LogError(object message) => Errors.Add(message.ToString());
    }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        public string name;
    }

    public static class SceneManager
    {
        public static string ActiveScene = "Field";
        public static Scene GetActiveScene() => new Scene { name = ActiveScene };
    }
}

public static class PlayData
{
    public static bool GameStarted;
    public static bool StoryPart;
    public static TempSaveData TSavedata;
    public static bool EncounterPresent;
    public static int Gold;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void GameEndInit() => GameStarted = false;
}

public sealed class TempSaveData
{
    public bool IsLoaded;
    public bool SwordSanctuary;
    public int StageNum = 3;
    public string NowStageMapKey = "TestMap";
    public bool EventUsed;
}

namespace GameDataEditor
{
    public sealed class GDEEnemyQueueData
    {
        public string Key;
        public GDEEnemyQueueData(string key) { Key = key; }
    }

    public static class GDEItemKeys
    {
        public const string EnemyQueue_Queue_DorchiX = "DorchiX";
        public const string EnemyQueue_Shiranui_Queue = "Shiranui";
        public const string EnemyQueue_Casino_Queue = "Casino";
    }
}

public sealed class FieldSystem
{
    public static FieldSystem instance;
    public bool BattleStartFlag;
    public int BattleRequests;
    public int BattlesStarted;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BattleStart(GDEEnemyQueueData QueueData)
    {
        BattleRequests++;
        if (!BattleStartFlag)
        {
            BattleStartFlag = true;
            BattlesStarted++;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BattleStart_MapEnemy()
    {
        BattleRequests++;
        if (!BattleStartFlag)
        {
            BattleStartFlag = true;
            BattlesStarted++;
        }
    }
}

public sealed class BattleSystem
{
    public static BattleSystem instance;
}

public sealed class SaveManager
{
    public static SaveManager savemanager;
    public int SaveAttempts;
    public int Checkpoints;
    public bool SavedEncounter;
    public bool SavedSwordSanctuary;
    public int SavedGold;
    public bool SavedEventUsed;
    public bool ThrowOnSave;
    public int QuitCalls;
    public int DeletedSaves;
    public int PermanentSaves;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ProgressOneSave()
    {
        SaveAttempts++;
        if (ThrowOnSave)
            throw new InvalidOperationException("Simulated save failure");
        Checkpoints++;
        SavedEncounter = PlayData.EncounterPresent;
        SavedSwordSanctuary = PlayData.TSavedata.SwordSanctuary;
        SavedGold = PlayData.Gold;
        SavedEventUsed = PlayData.TSavedata.EventUsed;
    }

    // These branches reproduce the relevant original QuitSave behavior from the
    // installed game: normal field saving, a live-battle early return, and cleanup.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void QuitSave()
    {
        savemanager.QuitCalls++;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene == "Main" || scene == "MainOption" ||
            (scene == "Battle" && BattleSystem.instance != null))
            return;
        if (scene == "Field" && PlayData.GameStarted && !PlayData.StoryPart)
        {
            savemanager.ProgressOneSave();
            return;
        }
        savemanager.DeletedSaves++;
        savemanager.PermanentSaves++;
    }
}

public sealed class RE_TrialofStrength
{
    public bool ThrowBeforeBattle;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void UseButton1()
    {
        if (PlayData.Gold < 500)
            return;
        PlayData.Gold -= 500;
        PlayData.TSavedata.EventUsed = true;
        if (ThrowBeforeBattle)
            throw new InvalidOperationException("Simulated trial failure");
        FieldSystem.instance.BattleStart(new GDEEnemyQueueData("Trial"));
    }
}

public sealed class RE_ShiranuiEvent
{
    public bool ThrowBeforeDelay;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void UseButton1()
    {
        PlayData.TSavedata.EventUsed = true;
        if (ThrowBeforeDelay)
            throw new InvalidOperationException("Simulated event failure");
        // The real coroutine returns to Unity before starting the battle.
    }

    public void FinishDelay() => FieldSystem.instance.BattleStart(
        new GDEEnemyQueueData(GDEItemKeys.EnemyQueue_Shiranui_Queue));
}

public sealed class RE_GrandCasino
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void UseButton2()
    {
        PlayData.Gold += 500;
        PlayData.TSavedata.EventUsed = true;
        // Rewards and event changes occur before the later battle coroutine.
    }

    public void FinishDelay() => FieldSystem.instance.BattleStart(
        new GDEEnemyQueueData(GDEItemKeys.EnemyQueue_Casino_Queue));
}

public sealed class BloodyMist
{
    public bool ThrowBeforeBattle;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void DoubleBattle()
    {
        if (ThrowBeforeBattle)
            throw new InvalidOperationException("Simulated chained battle failure");
        FieldSystem.instance.BattleStart(new GDEEnemyQueueData("SecondBoss"));
    }
}
