using System;
using System.Linq;
using System.Reflection;
using ChronoArkMod.Plugin;
using GameDataEditor;
using HarmonyLib;
using UnityEngine;

namespace SaveSaver
{
    [PluginConfig("SaveSaver", "zerol", "1.0.3")]
    public class SaveSaverPlugin : ChronoArkPlugin
    {
        public override void Initialize()
        {
            Checkpoint.Reset();
            _harmony = new Harmony(GetGuid());
            try
            {
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                var numPatched = _harmony.GetPatchedMethods().Count();
                Debug.Log($"SaveSaver: {numPatched} patched methods");
            }
            catch (Exception e)
            {
                // A partial install must not leave only half of the save protection active.
                _harmony.UnpatchSelf();
                Debug.LogError("SaveSaver patch failed: " + e);
            }
        }

        public override void Dispose()
        {
            _harmony?.UnpatchSelf();
            Checkpoint.Reset();
        }

        private Harmony _harmony;
    }

    [HarmonyPatch]
    public class Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FieldSystem), nameof(FieldSystem.BattleStart))]
        private static void AutoSave(FieldSystem __instance, GDEEnemyQueueData QueueData)
        {
            if (!Checkpoint.CanCapture(__instance) || QueueData == null)
                return;
            if (Checkpoint.ConsumePending(QueueData.Key))
                return;

            // BossEnter consumes this flag before calling BattleStart. The checkpoint
            // must still select DorchiX when the player enters the boss room again.
            Checkpoint.Capture(__instance, QueueData.Key == GDEItemKeys.EnemyQueue_Queue_DorchiX);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FieldSystem), nameof(FieldSystem.BattleStart_MapEnemy))]
        private static void AutoSaveMapEnemy(FieldSystem __instance)
        {
            if (!Checkpoint.CanCapture(__instance))
                return;
            Checkpoint.ConsumePending(null);
            Checkpoint.Capture(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.QuitSave))]
        private static bool QuitSavePrefix()
        {
            // During scene loading the active scene is still Field, but its encounter
            // has already been consumed. During shutdown BattleSystem may be destroyed
            // before QuitSave runs. In both cases keep the on-disk pre-battle save.
            return !Checkpoint.ShouldPreserve;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.ProgressOneSave))]
        private static bool ProgressOneSavePrefix()
        {
            // BattleEnd may launch a chained battle, then attempt its ordinary save.
            // Other battle-scene saves include Hope Mode defeat/return-to-Ark recovery.
            return !Checkpoint.IsTransitioning;
        }
    }
}
