using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace FFT.TrueHardcore
{
	[BepInPlugin("fierrof.fft.truehardcore", "FFT.TrueHardcore", "1.0.0")]
	public class TrueHardcoreLootAnimPatchPlugin : BaseUnityPlugin
	{
		private void Awake()
		{
			new Harmony("fierrof.fft.truehardcore").PatchAll();
			Logger.LogInfo("FFT.TrueHardcore loaded");
		}
	}

	[HarmonyPatch]
	public static class DisableCombatLootAnimationInjection
	{
		public static MethodBase TargetMethod()
		{
			Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault(a => string.Equals(a.GetName().Name, "HardcoreRebalance", StringComparison.OrdinalIgnoreCase));
			if (assembly == null)
			{
				return null;
			}
			// ...existing code...
			return null;
		}
		// ...existing code...
	}
}
