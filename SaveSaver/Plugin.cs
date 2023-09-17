using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using ChronoArkMod.Plugin;
using HarmonyLib;
using HarmonyLib.Tools;
using UnityEngine;

namespace SaveSaver
{
    [PluginConfig("SaveSaver", "zerol", "1.0.0")]
    public class SaveSaverPlugin : ChronoArkPlugin
    {
        public override void Initialize()
        {
            HarmonyFileLog.Enabled = true;
            _harmony = new Harmony(GetGuid());
            try
            {
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                var numPatched = _harmony.GetPatchedMethods().Count();
                Debug.Log($"SaveSaver: {numPatched} patched methods");
            }
            catch (Exception e)
            {
                Debug.LogError("SaveSaver patch failed: " + e);
            }
        }

        public override void Dispose()
        {
            _harmony?.UnpatchSelf();
        }

        private Harmony _harmony;
    }

    [HarmonyPatch]
    [HarmonyDebug]
    public class Patch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FieldSystem), nameof(FieldSystem.BattleStart))]
        private static void AutoSave()
        {
            HarmonyFileLog.Writer.WriteLine("SaveSaver: Save progress before battle");
            HarmonyFileLog.Writer.Flush();
            SaveManager.savemanager.ProgressOneSave();
        }


        [HarmonyTranspiler]
        [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.QuitSave))]
        private static IEnumerable<CodeInstruction> QuitSaveTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var idx = -1;
            for (var i = 0; i < codes.Count - 3; i++)
            {
                if (codes[i].opcode == OpCodes.Ldsfld && codes[i + 1].opcode == OpCodes.Callvirt &&
                    codes[i + 2].opcode == OpCodes.Ret)
                {
                    idx = i;
                    break;
                }
            }

            if (idx == -1)
            {
                HarmonyFileLog.Writer.WriteLine("SaveSaver: failed to patch SaveManager.QuitSave");
                HarmonyFileLog.Writer.Flush();
                throw new Exception("SaveSaver: failed to patch SaveManager.QuitSave");
            }

            codes[idx + 3].opcode = OpCodes.Ret;
            codes[idx + 3].operand = null;
            for (var i = idx + 4; i < codes.Count - 1; i++)
            {
                codes[i].opcode = OpCodes.Nop;
            }

            return codes;
        }
    }
}