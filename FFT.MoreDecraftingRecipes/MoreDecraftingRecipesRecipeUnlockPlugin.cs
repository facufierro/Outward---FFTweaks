using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace FFT.MoreDecraftingRecipes
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class MoreDecraftingRecipesRecipeUnlockPlugin : BaseUnityPlugin
	{
		private const string PluginGuid = "fierrof.fft.moredecraftingrecipes";
		private const string PluginName = "FFT.MoreDecraftingRecipes";
		private const string PluginVersion = "1.0.0";
		private const float RetryIntervalSeconds = 20f;

		private static readonly HashSet<int> ArrowItemIds = new HashSet<int>
		{
			5200001,
			5200002,
			5200003,
			5200004,
			5200005,
			5200007,
			5200008,
			5200009,
			5200010,
			5200019
		};

		private float _nextAttemptTime;
		private bool _hasLoggedSuccessfulUnlock;
		private static MoreDecraftingRecipesRecipeUnlockPlugin Instance;

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

		private void TryUnlockRecipesFromEquippedItem(object localCharacter, object equippedItem, string source)
		{
			if (localCharacter == null || equippedItem == null)
			{
				return;
			}

			if (!GetBoolMember(localCharacter, "IsLocalPlayer"))
			{
				return;
			}

			int equippedItemId = TryGetItemId(equippedItem);
			if (!ArrowItemIds.Contains(equippedItemId))
			{
				return;
			}

			try
			{
				int matchedRecipes = 0;
				int learnedRecipes = LearnArrowRecipesForItem(localCharacter, equippedItemId, ref matchedRecipes);

				if (matchedRecipes == 0)
				{
					return;
				}

				if (learnedRecipes > 0)
				{
					Logger.LogInfo($"[{source}] Learned {learnedRecipes} arrow decrafting recipe(s) for equipped arrow item id {equippedItemId}.");
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"[{source}] Equip-triggered recipe unlock failed: {ex.GetType().Name}: {ex.Message}");
			}
		}

		private void TryUnlockRecipes(string source)
		{
			try
			{
				List<object> localCharacters = GetLocalCharacters();
				if (localCharacters.Count == 0)
				{
					return;
				}

				int matchedRecipes = 0;
				int learnedRecipes = 0;
				foreach (object localCharacter in localCharacters)
				{
					foreach (object equippedItem in GetEquippedItems(localCharacter))
					{
						int equippedItemId = TryGetItemId(equippedItem);
						if (!ArrowItemIds.Contains(equippedItemId))
						{
							continue;
						}

						learnedRecipes += LearnArrowRecipesForItem(localCharacter, equippedItemId, ref matchedRecipes);
					}
				}

				if (matchedRecipes == 0)
				{
					Logger.LogWarning($"[{source}] No MadHoek arrow decrafting recipes matched equipped arrows yet.");
					return;
				}

				if (learnedRecipes > 0)
				{
					_hasLoggedSuccessfulUnlock = true;
					Logger.LogInfo($"[{source}] Arrow decrafting recipes processed. Matched: {matchedRecipes}, learned now: {learnedRecipes}.");
				}
				else if (!_hasLoggedSuccessfulUnlock)
				{
					Logger.LogInfo($"[{source}] Arrow decrafting recipes already known. Matched: {matchedRecipes}.");
					_hasLoggedSuccessfulUnlock = true;
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning($"[{source}] Recipe unlock attempt failed: {ex.GetType().Name}: {ex.Message}");
			}
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

		private static IEnumerable<object> GetEquippedItems(object localCharacter)
		{
			if (localCharacter == null)
			{
				yield break;
			}

			HashSet<int> seenItemIds = new HashSet<int>();

			object characterEquipment = GetMemberValue(localCharacter.GetType(), localCharacter, "CharacterEquipment")
				?? GetMemberValue(localCharacter.GetType(), localCharacter, "Equipment")
				?? GetMemberValue(localCharacter.GetType(), localCharacter, "m_equipment");

			if (characterEquipment == null)
			{
				yield break;
			}

			string[] slotCollections =
			{
				"m_equipmentSlots",
				"EquipmentSlots",
				"m_slots"
			};

			foreach (string collectionName in slotCollections)
			{
				object collection = GetMemberValue(characterEquipment.GetType(), characterEquipment, collectionName);
				if (!(collection is IEnumerable enumerable))
				{
					continue;
				}

				foreach (object slot in enumerable)
				{
					if (slot == null)
					{
						continue;
					}

					object equippedItem = GetMemberValue(slot.GetType(), slot, "EquippedItem")
						?? GetMemberValue(slot.GetType(), slot, "CurrentItem")
						?? GetMemberValue(slot.GetType(), slot, "m_equippedItem")
						?? GetMemberValue(slot.GetType(), slot, "m_item");

					if (equippedItem == null)
					{
						continue;
					}

					int itemId = TryGetItemId(equippedItem);
					if (itemId == int.MinValue || !seenItemIds.Add(itemId))
					{
						continue;
					}

					yield return equippedItem;
				}
			}
		}

		private static int LearnArrowRecipesForItem(object localCharacter, int equippedItemId, ref int matchedRecipes)
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

				if (!IsMadHoekArrowDisassembleRecipe(recipeUid, recipe, recipeType))
				{
					continue;
				}

				if (!RecipeUsesIngredientItemId(recipe, recipeType, equippedItemId))
				{
					continue;
				}

				matchedRecipes++;
				if (TryLearnRecipe(localCharacter, recipeUid, recipe, recipeType))
				{
					learnedRecipes++;
				}
			}

			return learnedRecipes;
		}

		private static bool IsMadHoekArrowDisassembleRecipe(string recipeUid, object recipe, Type recipeType)
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
			return normalized.Contains("madhoek")
				&& normalized.Contains("disassemble")
				&& normalized.Contains("arrow");
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

		private static IEnumerable<object> GetRecipeIngredients(object recipe, Type recipeType)
		{
			string[] memberCandidates =
			{
				"Ingredients",
				"m_ingredients",
				"m_ingredientStack",
				"m_ingredientStacks",
				"IngredientStack",
				"IngredientStacks"
			};

			foreach (string memberName in memberCandidates)
			{
				object raw = GetMemberValue(recipeType, recipe, memberName);
				if (raw is IEnumerable enumerable)
				{
					foreach (object item in enumerable)
					{
						yield return item;
					}

					yield break;
				}
			}
		}

		private static bool RecipeUsesIngredientItemId(object recipe, Type recipeType, int requiredItemId)
		{
			foreach (object ingredient in GetRecipeIngredients(recipe, recipeType))
			{
				if (ingredient == null)
				{
					continue;
				}

				int ingredientItemId = TryGetItemId(ingredient);
				if (ingredientItemId == requiredItemId)
				{
					return true;
				}
			}

			return false;
		}

		private static int TryGetItemId(object instance)
		{
			if (instance == null)
			{
				return int.MinValue;
			}

			Type type = instance.GetType();
			object value = GetMemberValue(type, instance, "ItemID")
				?? GetMemberValue(type, instance, "m_itemID")
				?? GetMemberValue(type, instance, "ItemId")
				?? GetMemberValue(type, instance, "SelectorValue");

			if (value == null)
			{
				return int.MinValue;
			}

			try
			{
				return Convert.ToInt32(value);
			}
			catch
			{
				return int.MinValue;
			}
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

		[HarmonyPatch]
		private static class CharacterEquipmentEquipItemPatch
		{
			private static IEnumerable<MethodBase> TargetMethods()
			{
				Type characterEquipmentType = FindTypeByName("CharacterEquipment");
				if (characterEquipmentType == null)
				{
					yield break;
				}

				foreach (MethodInfo method in characterEquipmentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
				{
					if (!string.Equals(method.Name, "EquipItem", StringComparison.Ordinal))
					{
						continue;
					}

					ParameterInfo[] parameters = method.GetParameters();
					if (parameters.Length != 2 || parameters[1].ParameterType != typeof(bool))
					{
						continue;
					}

					yield return method;
				}
			}

			private static void Postfix(object __instance, object __0)
			{
				if (Instance == null || __instance == null || __0 == null)
				{
					return;
				}

				object character = GetMemberValue(__instance.GetType(), __instance, "m_character")
					?? GetMemberValue(__instance.GetType(), __instance, "Character")
					?? GetMemberValue(__instance.GetType(), __instance, "OwnerCharacter");

				Instance.TryUnlockRecipesFromEquippedItem(character, __0, "CharacterEquipment.EquipItem");
			}
		}
	}
}
