using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace FFT.Knives_Master
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class KnivesMasterRecipeUnlockPlugin : BaseUnityPlugin
	{
		private const string PluginGuid = "fierrof.fft.knives_master";
		private const string PluginName = "FFT.Knives_Master";
		private const string PluginVersion = "1.0.0";
		private const float RetryIntervalSeconds = 20f;

		private static readonly string[] KnivesMasterMarkers =
		{
			"daggertoknife",
			"knifetodagger"
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

			if (!IsKnivesMasterInstalled())
			{
				return;
			}

			int equippedItemId = TryGetItemId(equippedItem);
			string equippedItemKey = GetItemKeyFromInstance(equippedItem);
			string conversionDirection = GetConversionDirection(equippedItemKey);

			if (equippedItemId == int.MinValue && string.IsNullOrWhiteSpace(equippedItemKey))
			{
				return;
			}

			Logger.LogInfo($"[{source}] Equip detected. ItemId: {equippedItemId}, Key: {equippedItemKey}, Direction: {conversionDirection}");

			try
			{
				int matchedRecipes = 0;
				int learnedRecipes = LearnConversionRecipesForItem(localCharacter, equippedItemId, equippedItemKey, conversionDirection, ref matchedRecipes);

				if (matchedRecipes == 0)
				{
					return;
				}

				if (learnedRecipes > 0)
				{
					Logger.LogInfo($"[{source}] Learned {learnedRecipes} conversion recipe(s) for equipped item id {equippedItemId}.");
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
				int learnedRecipes = 0;
				foreach (object localCharacter in localCharacters)
				{
					foreach (object equippedItem in GetEquippedItems(localCharacter))
					{
						int equippedItemId = TryGetItemId(equippedItem);
						string equippedItemKey = GetItemKeyFromInstance(equippedItem);
						string conversionDirection = GetConversionDirection(equippedItemKey);

						if (equippedItemId == int.MinValue && string.IsNullOrWhiteSpace(equippedItemKey))
						{
							continue;
						}

						learnedRecipes += LearnConversionRecipesForItem(localCharacter, equippedItemId, equippedItemKey, conversionDirection, ref matchedRecipes);
					}
				}

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
					if (!HasRequiredConversionItem(localCharacter, recipe, recipeType))
					{
						continue;
					}

					if (TryLearnRecipe(localCharacter, recipeUid, recipe, recipeType))
					{
						learnedRecipes++;
					}
				}
			}

			return learnedRecipes;
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

		private static int LearnConversionRecipesForItem(object localCharacter, int equippedItemId, string equippedItemKey, string conversionDirection, ref int matchedRecipes)
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

				if (!RecipeUsesIngredientItemId(recipe, recipeType, equippedItemId)
					&& !RecipeMatchesEquippedConversion(recipeUid, recipe, recipeType, equippedItemKey, conversionDirection))
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

			if (normalized.Contains("toknife") || normalized.Contains("todagger"))
			{
				return true;
			}

			bool hasConversionKeyword = normalized.Contains("convert") || normalized.Contains("transform");
			bool hasKnifeOrDagger = normalized.Contains("knife") || normalized.Contains("dagger");
			return hasConversionKeyword && hasKnifeOrDagger;
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

		private static bool HasRequiredConversionItem(object localCharacter, object recipe, Type recipeType)
		{
			object inventory = GetMemberValue(localCharacter.GetType(), localCharacter, "Inventory");
			if (inventory == null)
			{
				return false;
			}

			foreach (object ingredient in GetRecipeIngredients(recipe, recipeType))
			{
				if (ingredient == null)
				{
					continue;
				}

				Type ingredientType = ingredient.GetType();
				object itemIdValue = GetMemberValue(ingredientType, ingredient, "ItemID")
					?? GetMemberValue(ingredientType, ingredient, "m_itemID")
					?? GetMemberValue(ingredientType, ingredient, "ItemId");

				if (itemIdValue == null)
				{
					continue;
				}

				int itemId;
				try
				{
					itemId = Convert.ToInt32(itemIdValue);
				}
				catch
				{
					continue;
				}

				if (InventoryOwnsItem(inventory, itemId))
				{
					return true;
				}
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

		private static bool RecipeMentionsEquippedItem(string recipeUid, object recipe, Type recipeType, string equippedItemKey)
		{
			return false;
		}

		private static bool RecipeMatchesEquippedConversion(string recipeUid, object recipe, Type recipeType, string equippedItemKey, string conversionDirection)
		{
			if (string.IsNullOrWhiteSpace(equippedItemKey) || string.IsNullOrWhiteSpace(conversionDirection))
			{
				return false;
			}

			HashSet<string> candidateFamilies = new HashSet<string>(GetCandidateNameTokens(equippedItemKey));
			if (candidateFamilies.Count == 0)
			{
				return false;
			}

			List<string> recipeTexts = new List<string>
			{
				recipeUid,
				GetStringMember(recipeType, recipe, "Name"),
				GetStringMember(recipeType, recipe, "RecipeName"),
				GetStringMember(recipeType, recipe, "UID"),
				GetStringMember(recipeType, recipe, "m_name"),
				GetStringMember(recipeType, recipe, "m_recipeName")
			};

			if (recipe is UnityEngine.Object unityObject)
			{
				recipeTexts.Add(unityObject.name);
			}

			foreach (string text in recipeTexts.Where(t => !string.IsNullOrWhiteSpace(t)))
			{
				if (!TryExtractRecipeFamily(text, out string recipeFamily, out string recipeDirection))
				{
					continue;
				}

				if (!string.Equals(recipeDirection, conversionDirection, StringComparison.Ordinal))
				{
					continue;
				}

				if (candidateFamilies.Contains(recipeFamily))
				{
					return true;
				}
			}

			return false;
		}

		private static string GetItemKeyFromInstance(object item)
		{
			if (item == null)
			{
				return string.Empty;
			}

			Type itemType = item.GetType();
			string itemKey = GetStringMember(itemType, item, "Name")
				?? GetStringMember(itemType, item, "DisplayName")
				?? GetStringMember(itemType, item, "UID")
				?? GetStringMember(itemType, item, "m_name");

			return Normalize(itemKey);
		}

		private static string GetConversionDirection(string equippedItemKey)
		{
			if (string.IsNullOrWhiteSpace(equippedItemKey))
			{
				return string.Empty;
			}

			bool hasDagger = equippedItemKey.Contains("dagger");
			bool hasKnife = equippedItemKey.Contains("knife");

			if (hasDagger && !hasKnife)
			{
				return "daggertoknife";
			}

			if (hasKnife && !hasDagger)
			{
				return "knifetodagger";
			}

			return string.Empty;
		}

		private static IEnumerable<string> GetCandidateNameTokens(string equippedItemKey)
		{
			HashSet<string> tokens = new HashSet<string>();
			if (string.IsNullOrWhiteSpace(equippedItemKey))
			{
				yield break;
			}

			tokens.Add(equippedItemKey);

			string baseName = equippedItemKey;
			if (baseName.StartsWith("crafting"))
			{
				baseName = baseName.Substring("crafting".Length);
			}

			if (baseName.EndsWith("dagger"))
			{
				baseName = baseName.Substring(0, baseName.Length - "dagger".Length);
			}
			else if (baseName.EndsWith("knife"))
			{
				baseName = baseName.Substring(0, baseName.Length - "knife".Length);
			}

			baseName = Normalize(baseName);
			if (!string.IsNullOrWhiteSpace(baseName))
			{
				tokens.Add(baseName);
			}

			if (baseName == "junk" || baseName == "shank" || baseName == "shiv")
			{
				tokens.Add("shiv");
				tokens.Add("shank");
				tokens.Add("junk");
			}

			foreach (string token in tokens)
			{
				if (!string.IsNullOrWhiteSpace(token))
				{
					yield return token;
				}
			}
		}

		private static bool TryExtractRecipeFamily(string value, out string family, out string direction)
		{
			family = string.Empty;
			direction = string.Empty;

			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}

			Match match = Regex.Match(value, "!?(?<family>[^-!]+)-(?<dir>daggerTOknife|knifeTOdagger)-custom", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			if (!match.Success)
			{
				return false;
			}

			family = Normalize(match.Groups["family"].Value);
			direction = Normalize(match.Groups["dir"].Value);
			return !string.IsNullOrWhiteSpace(family) && !string.IsNullOrWhiteSpace(direction);
		}

		private static bool InventoryOwnsItem(object inventory, int itemId)
		{
			Type inventoryType = inventory.GetType();

			MethodInfo ownsItemMethod = inventoryType.GetMethod("OwnsItem", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
			if (ownsItemMethod != null)
			{
				object owns = ownsItemMethod.Invoke(inventory, new object[] { itemId });
				if (owns is bool b && b)
				{
					return true;
				}
			}

			MethodInfo itemCountMethod = inventoryType.GetMethod("ItemCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
			if (itemCountMethod != null)
			{
				object count = itemCountMethod.Invoke(inventory, new object[] { itemId });
				if (count != null)
				{
					try
					{
						if (Convert.ToInt32(count) > 0)
						{
							return true;
						}
					}
					catch
					{
					}
				}
			}

			MethodInfo getOwnedItemsMethod = inventoryType.GetMethod("GetOwnedItems", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
			if (getOwnedItemsMethod != null)
			{
				object ownedItems = getOwnedItemsMethod.Invoke(inventory, new object[] { itemId });
				if (ownedItems is IEnumerable enumerable)
				{
					foreach (object item in enumerable)
					{
						if (item != null)
						{
							return true;
						}
					}
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
				?? GetMemberValue(type, instance, "ItemId");

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
