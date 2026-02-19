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
		private const string PluginVersion = "0.1.0";

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
		private bool _startupUnlockAttempted;

		private void Awake()
		{
			new Harmony(PluginGuid).PatchAll();
			Logger.LogInfo($"{PluginName} loaded");
		}

		private void Update()
		{
			if (_startupUnlockAttempted || Time.unscaledTime < _nextAttemptTime)
			{
				return;
			}

			_nextAttemptTime = Time.unscaledTime + 5f;

			if (!IsKnivesMasterInstalled())
			{
				return;
			}

			object localCharacter = TryGetLocalCharacter();
			if (localCharacter == null)
			{
				return;
			}

			int matchedRecipes = 0;
			int learnedRecipes = LearnKnivesMasterDaggerRecipes(localCharacter, ref matchedRecipes);

			if (matchedRecipes == 0)
			{
				Logger.LogWarning("Knives Master detected, but no dagger recipe assets were matched yet.");
				return;
			}

			_startupUnlockAttempted = true;
			Logger.LogInfo($"Knives Master dagger recipes processed on login. Matched: {matchedRecipes}, learned now: {learnedRecipes}.");
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

		private static object TryGetLocalCharacter()
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

		private static int LearnKnivesMasterDaggerRecipes(object localCharacter, ref int matchedRecipes)
		{
			Type recipeType = FindTypeByName("Recipe");
			if (recipeType == null)
			{
				return 0;
			}

			Array allRecipes = Resources.FindObjectsOfTypeAll(recipeType);
			int learnedRecipes = 0;

			foreach (object recipe in allRecipes)
			{
				if (!IsKnivesMasterDaggerRecipe(recipe, recipeType))
				{
					continue;
				}

				matchedRecipes++;
				if (TryLearnRecipe(localCharacter, recipe, recipeType))
				{
					learnedRecipes++;
				}
			}

			return learnedRecipes;
		}

		private static bool IsKnivesMasterDaggerRecipe(object recipe, Type recipeType)
		{
			List<string> parts = new List<string>();

			if (recipe is UnityEngine.Object unityObject)
			{
				parts.Add(unityObject.name);
			}

			parts.Add(GetStringMember(recipeType, recipe, "Name"));
			parts.Add(GetStringMember(recipeType, recipe, "RecipeName"));
			parts.Add(GetStringMember(recipeType, recipe, "UID"));
			parts.Add(GetStringMember(recipeType, recipe, "m_name"));
			parts.Add(GetStringMember(recipeType, recipe, "m_recipeName"));

			string key = string.Join(" | ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToLowerInvariant();
			if (key.Length == 0 || !key.Contains("dagger"))
			{
				return false;
			}

			return KnivesMasterMarkers.Any(marker => key.Contains(marker));
		}

		private static bool TryLearnRecipe(object localCharacter, object recipe, Type recipeType)
		{
			Type characterType = localCharacter.GetType();
			MethodInfo[] methods = characterType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			foreach (MethodInfo method in methods)
			{
				if (method.Name.IndexOf("learn", StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}

				ParameterInfo[] parameters = method.GetParameters();
				if (parameters.Length != 1 || !parameters[0].ParameterType.IsAssignableFrom(recipeType))
				{
					continue;
				}

				object result = method.Invoke(localCharacter, new[] { recipe });
				return result is bool b ? b : true;
			}

			object recipeUid = GetMemberValue(recipeType, recipe, "UID")
				?? GetMemberValue(recipeType, recipe, "RecipeID")
				?? GetMemberValue(recipeType, recipe, "m_recipeID");

			foreach (MethodInfo method in methods)
			{
				if (method.Name.IndexOf("learnrecipe", StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}

				ParameterInfo[] parameters = method.GetParameters();
				if (parameters.Length != 1 || recipeUid == null)
				{
					continue;
				}

				try
				{
					object converted = Convert.ChangeType(recipeUid, parameters[0].ParameterType);
					object result = method.Invoke(localCharacter, new[] { converted });
					return result is bool b ? b : true;
				}
				catch
				{
				}
			}

			return false;
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
	}
}
