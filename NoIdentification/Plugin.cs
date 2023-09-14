using System;
using System.Linq;
using System.Reflection;
using ChronoArkMod.Plugin;
using HarmonyLib;
using UnityEngine;


namespace NoIdentification
{
    [PluginConfig("NoIdentification", "zerol", "1.0.0")]
    public class NoIdentificationPlugin : ChronoArkPlugin
    {
        public override void Initialize()
        {
            _harmony = new Harmony(GetGuid());
            try
            {
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                var numPatched = _harmony.GetPatchedMethods().Count();
                Debug.Log($"NoIdentificationPlugin: {numPatched} patched methods");
            }
            catch (Exception e)
            {
                Debug.LogError("NoIdentificationPlugin PatchAll failed: " + e);
            }
        }

        public override void Dispose()
        {
            _harmony?.UnpatchSelf();
        }

        private Harmony _harmony;
    }

    [HarmonyPatch]
    public class Patch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Item_Scroll), "IsIdentify", MethodType.Getter)]
        [HarmonyPatch(typeof(Item_Potions), "IsIdentify", MethodType.Getter)]
        [HarmonyPatch(typeof(Item_Equip), "IsIdentify", MethodType.Getter)]
        private static void PatchIsIdentify(ref bool __result)
        {
            __result = true;
        }
    }
}