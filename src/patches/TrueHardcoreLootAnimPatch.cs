using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace FFT.TrueHardcore
{
    [BepInDependency("com.iggy.hardcorere", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("fierrof.fft.truehardcore", "FFT.TrueHardcore", "1.0.0")]
    public class TrueHardcoreLootAnimPatch : BaseUnityPlugin
    {
        private const string TrueHardcoreHarmonyId = "com.iggy.hardcorere";
        private const string TargetPrefixType = "HardcoreRebalance.CombatAnims+InteractionTriggerBase_TryActivateBasicAction";

        private void Awake()
        {
            MethodBase target = AccessTools.Method(
                typeof(InteractionTriggerBase),
                "TryActivateBasicAction",
                new[] { typeof(Character), typeof(int) });

            if (target == null)
            {
                Logger.LogError("Failed to find InteractionTriggerBase.TryActivateBasicAction(Character,int) target method.");
                return;
            }

            Patches patches = Harmony.GetPatchInfo(target);
            if (patches?.Prefixes == null || patches.Prefixes.Count == 0)
            {
                Logger.LogWarning("No prefixes found on TryActivateBasicAction; nothing to disable.");
                return;
            }

            int removed = 0;
            foreach (Patch prefix in patches.Prefixes)
            {
                if (!IsTargetPrefix(prefix))
                {
                    continue;
                }

                Harmony.UnpatchID(TrueHardcoreHarmonyId, prefix.PatchMethod);
                removed++;
            }

            Logger.LogInfo($"Disabled TrueHardcore loot interaction prefix patches: {removed}");
        }

        private static bool IsTargetPrefix(Patch prefix)
        {
            if (prefix?.PatchMethod == null)
            {
                return false;
            }

            return string.Equals(prefix.owner, TrueHardcoreHarmonyId, StringComparison.Ordinal)
                && string.Equals(prefix.PatchMethod.DeclaringType?.FullName, TargetPrefixType, StringComparison.Ordinal);
        }
    }
}
