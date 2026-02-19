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

			Type patchType = assembly.GetType("HardcoreRebalance.CombatAnims+InteractionTriggerBase_TryActivateBasicAction", false);
			return patchType?.GetMethod("Prefix", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
		}

		public static bool Prefix(object __instance, object _character)
		{
			if (_character == null)
			{
				return true;
			}

			if (!GetBoolMember(_character, "IsLocalPlayer") || !GetBoolMember(_character, "InCombat"))
			{
				return true;
			}

			if (!IsLootInteraction(__instance))
			{
				return true;
			}

			return false;
		}

		private static bool IsLootInteraction(object interactionTriggerBase)
		{
			if (interactionTriggerBase == null)
			{
				return false;
			}

			object triggerManager = GetMemberValue(interactionTriggerBase.GetType(), interactionTriggerBase, "CurrentTriggerManager");
			if (triggerManager == null || !string.Equals(triggerManager.GetType().Name, "InteractionActivator", StringComparison.Ordinal))
			{
				return false;
			}

			object basicInteraction = GetMemberValue(triggerManager.GetType(), triggerManager, "BasicInteraction");
			if (basicInteraction == null)
			{
				return false;
			}

			string interactionType = basicInteraction.GetType().Name;
			return string.Equals(interactionType, "InteractionOpenContainer", StringComparison.Ordinal)
				|| string.Equals(interactionType, "InteractionTake", StringComparison.Ordinal);
		}

		private static bool GetBoolMember(object instance, string memberName)
		{
			object value = GetMemberValue(instance.GetType(), instance, memberName);
			return value is bool b && b;
		}

		private static object GetMemberValue(Type ownerType, object instance, string memberName)
		{
			PropertyInfo property = ownerType.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
			if (property != null)
			{
				return property.GetValue(instance);
			}

			FieldInfo field = ownerType.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
			return field?.GetValue(instance);
		}
	}
}
