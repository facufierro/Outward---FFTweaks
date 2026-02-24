using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace FFT.TrueHardcore
{
	[BepInDependency("com.iggy.hardcorere", BepInDependency.DependencyFlags.HardDependency)]
	[BepInPlugin("fierrof.fft.truehardcore", "FFT.TrueHardcore", "1.0.0")]
	public class TrueHardcoreLootAnimPatchPlugin : BaseUnityPlugin
	{
		private const string TrueHardcoreHarmonyId = "com.iggy.hardcorere";
		private Harmony _harmony;

		private void Awake()
		{
			_harmony = new Harmony("fierrof.fft.truehardcore");
			_harmony.PatchAll();
			DisableTrueHardcoreLootPrefix();
			Logger.LogInfo("FFT.TrueHardcore loaded");
		}

		private void DisableTrueHardcoreLootPrefix()
		{
			MethodBase target = AccessTools.Method(typeof(InteractionTriggerBase), "TryActivateBasicAction", new Type[]
			{
				typeof(Character),
				typeof(int)
			});

			if (target == null)
			{
				Logger.LogError("Failed to find InteractionTriggerBase.TryActivateBasicAction(Character,int) target method.");
				return;
			}

			Patches patches = Harmony.GetPatchInfo(target);
			if (patches == null || patches.Prefixes == null || patches.Prefixes.Count == 0)
			{
				Logger.LogWarning("No prefixes found on TryActivateBasicAction; nothing to disable.");
				return;
			}

			int removed = 0;
			for (int i = patches.Prefixes.Count - 1; i >= 0; i--)
			{
				var prefix = patches.Prefixes[i];
				if (!string.Equals(prefix.owner, TrueHardcoreHarmonyId, StringComparison.Ordinal))
				{
					continue;
				}

				if (prefix.PatchMethod == null)
				{
					continue;
				}

				if (!string.Equals(prefix.PatchMethod.DeclaringType?.FullName, "HardcoreRebalance.CombatAnims+InteractionTriggerBase_TryActivateBasicAction", StringComparison.Ordinal))
				{
					continue;
				}

				_harmony.Unpatch(target, prefix.PatchMethod);
				removed++;
			}

			Logger.LogInfo($"Disabled TrueHardcore loot interaction prefix patches: {removed}");
		}
	}
}
