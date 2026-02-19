using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace FFT.KnivesMaster
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class KnivesMasterRecipeUnlockPlugin : BaseUnityPlugin
	{
		private const string PluginGuid = "fierrof.fft.knivesmaster";
		private const string PluginName = "FFT.KnivesMaster";
		private const string PluginVersion = "1.0.0";
		private const float RetryIntervalSeconds = 20f;

		private static readonly string[] KnivesMasterMarkers =
		{
			"daggertoknife",
			"knifetodagger",
			"chalcedony dagger",
			"crescent dagger",
			"damascene dagger",
			"fang dagger",
			"horror dagger",
			"obsidian dagger",
			"tsar dagger"
		};

		private float _nextAttemptTime;
		private bool _hasLoggedSuccessfulUnlock;
		private static KnivesMasterRecipeUnlockPlugin Instance;

		private void Awake()
		{
			Instance = this;
			new Harmony(PluginGuid).PatchAll();
			Logger.LogInfo($"{PluginName} loaded");
			TryUnlockRecipes("Awake");
		}

		private void Update()
		{
			if (Time.unscaledTime < _nextAttemptTime)
			{
				return;
			}

			_nextAttemptTime = Time.unscaledTime + RetryIntervalSeconds;
			TryUnlockRecipes("UpdateRetry");
		}

		private void TryUnlockRecipes(string source)
		{
			try
			{
				if (!IsKnivesMasterInstalled())
				{
					return;
				}

				List<object> localCharacters = GetLocalCharacters();
				if (localCharacters.Count == 0)
				{
					return;
				}

				int matchedRecipes = 0;
				int learnedRecipes = LearnKnivesMasterDaggerRecipes(localCharacters, ref matchedRecipes);

				if (matchedRecipes == 0)
				{
					Logger.LogWarning($"[{source}] Knives Master detected, but no dagger recipes matched yet.");
					return;
				}

				if (learnedRecipes > 0)
				{
					_hasLoggedSuccessfulUnlock = true;
					Logger.LogInfo($"[{source}] Knives Master dagger recipes processed. Matched: {matchedRecipes}, learned now: {learnedRecipes}.");
				}
				else if (!_hasLoggedSuccessfulUnlock)
				{
					Logger.LogInfo($"[{source}] Knives Master dagger recipes already known. Matched: {matchedRecipes}.");
					_hasLoggedSuccessfulUnlock = true;
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"[{source}] Recipe unlock attempt failed: {ex.GetType().Name}: {ex.Message}");
			}
		}

		private static bool IsKnivesMasterInstalled()
		{
			return AppDomain.CurrentDomain.GetAssemblies().Any(a =>
			{
				string name = a.GetName().Name;
				return name.IndexOf("ColorPickerKnife", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("Knives_Master", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("Knives", StringComparison.OrdinalIgnoreCase) >= 0;
			});
		}

		private static List<object> GetLocalCharacters()
		{
			List<object> result = new List<object>();

			foreach (object playerSystem in EnumerateLocalPlayerSystems())
			{
				object controlledCharacter = GetMemberValue(playerSystem.GetType(), playerSystem, "ControlledCharacter");
				if (controlledCharacter != null)
				{
					result.Add(controlledCharacter);
				}
			}

			if (result.Count > 0)
			{
				return result;
			}

			object fallbackCharacter = TryGetFirstLocalCharacter();
			if (fallbackCharacter != null)
			{
				result.Add(fallbackCharacter);
			}

			return result;
		}

		private static IEnumerable<object> EnumerateLocalPlayerSystems()
		{
			Type globalType = FindTypeByName("Global");
			object lobby = globalType == null ? null : GetMemberValue(globalType, null, "Lobby");
			if (lobby == null)
			{
				yield break;
			}

			object players = GetMemberValue(lobby.GetType(), lobby, "PlayersInLobby");
			if (!(players is IEnumerable enumerable))
			{
				yield break;
			}

			foreach (object playerSystem in enumerable)
			{
				if (playerSystem == null)
				{
					continue;
				}

				if (GetBoolMember(playerSystem, "IsLocalPlayer"))
				{
					yield return playerSystem;
				}
			}
		}

		private static object TryGetFirstLocalCharacter()
		{
			Type characterManagerType = FindTypeByName("CharacterManager");
			if (characterManagerType == null)
			{
				return null;
			}

			object manager = GetMemberValue(characterManagerType, null, "Instance")
				?? GetMemberValue(characterManagerType, null, "m_instance")
				?? GetMemberValue(characterManagerType, null, "instance");

			if (manager == null)
			{
				return null;
			}

			return InvokeNoArg(manager, "GetFirstLocalCharacter")
				?? GetMemberValue(characterManagerType, manager, "m_localCharacter")
				?? GetMemberValue(characterManagerType, manager, "m_localPlayer")
				?? GetMemberValue(characterManagerType, manager, "LocalCharacter");
		}

		private static int LearnKnivesMasterDaggerRecipes(IReadOnlyList<object> localCharacters, ref int matchedRecipes)
		{
			Type recipeType = FindTypeByName("Recipe");
			if (recipeType == null)
			{
				return 0;
			}

			Array allRecipes = Resources.FindObjectsOfTypeAll(recipeType);
			if (allRecipes == null || allRecipes.Length == 0)
			{
				return 0;
			}

			int learnedRecipes = 0;

			foreach (object recipe in allRecipes)
			{
				if (recipe == null)
				{
					continue;
				}

				string recipeUid = GetStringMember(recipeType, recipe, "UID")
					?? GetStringMember(recipeType, recipe, "RecipeID")
					?? GetStringMember(recipeType, recipe, "m_recipeID");

				if (!IsKnivesMasterDaggerRecipe(recipeUid, recipe, recipeType))
				{
					continue;
				}

				matchedRecipes++;
				foreach (object localCharacter in localCharacters)
				{
					if (TryLearnRecipe(localCharacter, recipeUid, recipe, recipeType))
					{
						learnedRecipes++;
					}
				}
			}

			return learnedRecipes;
		}

		private static bool IsKnivesMasterDaggerRecipe(string recipeUid, object recipe, Type recipeType)
		{
			List<string> parts = new List<string>();
			parts.Add(recipeUid);

			if (recipe is UnityEngine.Object unityObject)
			{
				parts.Add(unityObject.name);
			}

			parts.Add(GetStringMember(recipeType, recipe, "Name"));
			parts.Add(GetStringMember(recipeType, recipe, "RecipeName"));
			parts.Add(GetStringMember(recipeType, recipe, "UID"));
			parts.Add(GetStringMember(recipeType, recipe, "m_name"));
			parts.Add(GetStringMember(recipeType, recipe, "m_recipeName"));

			string key = string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
			if (key.Length == 0)
			{
				return false;
			}

			string normalized = Normalize(key);
			if (KnivesMasterMarkers.Any(marker => normalized.Contains(Normalize(marker))))
			{
				return true;
			}

			bool knivesMasterSignature = normalized.Contains("knivesmaster") || normalized.Contains("stormcancer") || normalized.Contains("colorpickerknife");
			bool daggerOrKnife = normalized.Contains("dagger") || normalized.Contains("knife");
			return knivesMasterSignature && daggerOrKnife;
		}

		private static bool TryLearnRecipe(object localCharacter, string recipeUid, object recipe, Type recipeType)
		{
			object inventory = GetMemberValue(localCharacter.GetType(), localCharacter, "Inventory");
			if (inventory == null)
			{
				return false;
			}

			object recipeKnowledge = GetMemberValue(inventory.GetType(), inventory, "RecipeKnowledge");
			if (recipeKnowledge == null)
			{
				return false;
			}

			if (!string.IsNullOrWhiteSpace(recipeUid) && IsRecipeAlreadyLearned(recipeKnowledge, recipeUid))
			{
				return false;
			}

			MethodInfo learnRecipeMethod = recipeKnowledge.GetType()
				.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.FirstOrDefault(m => string.Equals(m.Name, "LearnRecipe", StringComparison.OrdinalIgnoreCase) && m.GetParameters().Length == 1 && recipe != null && m.GetParameters()[0].ParameterType.IsAssignableFrom(recipeType));

			if (learnRecipeMethod != null)
			{
				object result = learnRecipeMethod.Invoke(recipeKnowledge, new[] { recipe });
				return result is bool b ? b : true;
			}

			MethodInfo learnRecipeUidMethod = recipeKnowledge.GetType()
				.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.FirstOrDefault(m => string.Equals(m.Name, "LearnRecipe", StringComparison.OrdinalIgnoreCase) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));

			if (learnRecipeUidMethod != null && !string.IsNullOrWhiteSpace(recipeUid))
			{
				object result = learnRecipeUidMethod.Invoke(recipeKnowledge, new object[] { recipeUid });
				return result is bool b ? b : true;
			}

			return false;
		}

		private static bool IsRecipeAlreadyLearned(object recipeKnowledge, string recipeUid)
		{
			MethodInfo method = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.FirstOrDefault(m => string.Equals(m.Name, "IsRecipeLearned", StringComparison.OrdinalIgnoreCase)
					&& m.GetParameters().Length == 1
					&& m.GetParameters()[0].ParameterType == typeof(string));

			if (method == null)
			{
				return false;
			}

			object result = method.Invoke(recipeKnowledge, new object[] { recipeUid });
			return result is bool b && b;
		}

		private static Type FindTypeByName(string typeName)
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type type = assembly.GetTypes().FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.Ordinal));
				if (type != null)
				{
					return type;
				}
			}

			return null;
		}

		private static object InvokeNoArg(object instance, string methodName)
		{
			MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
			return method?.Invoke(instance, null);
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

		private static string GetStringMember(Type ownerType, object instance, string memberName)
		{
			object value = GetMemberValue(ownerType, instance, memberName);
			return value?.ToString();
		}

		private static string Normalize(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}

			char[] chars = value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
			return new string(chars);
		}

		[HarmonyPatch]
		private static class GameplayResumePatch
		{
			private static MethodBase TargetMethod()
			{
				Type networkLoaderType = FindTypeByName("NetworkLevelLoader");
				return networkLoaderType?.GetMethod("UnPauseGameplay", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
			}

			private static void Postfix()
			{
				Instance?.TryUnlockRecipes("NetworkLevelLoader.UnPauseGameplay");
			}
		}
	}
}
