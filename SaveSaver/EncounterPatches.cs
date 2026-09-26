using System;
using GameDataEditor;
using HarmonyLib;

namespace SaveSaver
{
    [HarmonyPatch(typeof(RE_TrialofStrength), nameof(RE_TrialofStrength.UseButton1))]
    internal static class TrialCheckpointPatch
    {
        [HarmonyPrefix]
        private static void Prefix(out bool __state)
        {
            __state = false;
            if (!Checkpoint.CanCapture(FieldSystem.instance) || PlayData.Gold < 500)
                return;
            // Capture before the entry fee is charged, then suppress the inner save.
            Checkpoint.Capture(FieldSystem.instance);
            Checkpoint.Suppress();
            __state = true;
        }

        [HarmonyFinalizer]
        private static void Finalizer(bool __state)
        {
            if (__state)
                Checkpoint.EndSuppression();
        }
    }

    [HarmonyPatch]
    internal static class DelayedEncounterCheckpointPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(RE_ShiranuiEvent), nameof(RE_ShiranuiEvent.UseButton1))]
        private static void ShiranuiPrefix(out bool __state)
        {
            __state = Checkpoint.BeginPending(GDEItemKeys.EnemyQueue_Shiranui_Queue);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RE_GrandCasino), nameof(RE_GrandCasino.UseButton2))]
        private static void CasinoPrefix(out bool __state)
        {
            // Co_Enter grants gold and disables the event before its delayed battle.
            __state = Checkpoint.BeginPending(GDEItemKeys.EnemyQueue_Casino_Queue);
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(RE_ShiranuiEvent), nameof(RE_ShiranuiEvent.UseButton1))]
        [HarmonyPatch(typeof(RE_GrandCasino), nameof(RE_GrandCasino.UseButton2))]
        private static void Finalizer(bool __state, Exception __exception)
        {
            if (__state && __exception != null)
                Checkpoint.ClearPending();
        }
    }

    [HarmonyPatch(typeof(BloodyMist), nameof(BloodyMist.DoubleBattle))]
    internal static class ChainedBattleCheckpointPatch
    {
        [HarmonyPrefix]
        private static void Prefix(out bool __state)
        {
            Checkpoint.Suppress();
            __state = true;
        }

        [HarmonyFinalizer]
        private static void Finalizer(bool __state)
        {
            if (__state)
                Checkpoint.EndSuppression();
        }
    }

    [HarmonyPatch(typeof(PlayData), nameof(PlayData.GameEndInit))]
    internal static class EndRunCheckpointPatch
    {
        [HarmonyPrefix]
        private static void Prefix() => Checkpoint.ClearPending();
    }
}
