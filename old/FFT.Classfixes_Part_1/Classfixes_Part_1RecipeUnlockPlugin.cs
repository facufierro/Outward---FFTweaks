using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace FFT.Classfixes_Part_1
{
	[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
	public class ClassfixesPart1RecipeUnlockPlugin : BaseUnityPlugin
	{
		private const string PluginGuid = "fierrof.fft.classfixes_part_1";
		private const string PluginName = "FFT.Classfixes_Part_1";
		private const string PluginVersion = "1.0.0";
		private const float RetryIntervalSeconds = 20f;

		private static ClassfixesPart1RecipeUnlockPlugin Instance;
		private float _nextAttemptTime;

		private void Awake()
		{
			Instance = this;
			new Harmony(PluginGuid).PatchAll();
			Logger.LogInfo($"{PluginName} loaded");
		}

		private void Update()
		{
			if (Time.unscaledTime < _nextAttemptTime)
			{
				return;
			}

			_nextAttemptTime = Time.unscaledTime + RetryIntervalSeconds;
			TryUnlockFromEquippedItems("UpdateRetry");
		}

		private void TryUnlockFromEquippedItems(string source)
		{
			if (!IsClassfixesPart1Installed())
			{
				return;
			}

			object localCharacter = TryGetFirstLocalCharacter();
			if (localCharacter == null || !GetBoolMember(localCharacter, "IsLocalPlayer"))
			{
				return;
			}

			foreach (object equippedItem in GetEquippedItems(localCharacter))
			{
				TryUnlockFromEquippedItem(localCharacter, equippedItem, source);
			}
		}

		private void TryUnlockFromEquippedItem(object localCharacter, object equippedItem, string source)
		{
			if (localCharacter == null || equippedItem == null)
			{
				return;
			}

			string itemKey = GetItemKeyFromInstance(equippedItem);
			if (string.IsNullOrWhiteSpace(itemKey))
			{
				return;
			}

			string direction = GetConversionDirection(itemKey);
			if (string.IsNullOrWhiteSpace(direction))
			{
				return;
			}

			int learned = LearnMatchingConversionRecipe(localCharacter, itemKey, direction);
			if (learned > 0)
			{
				Logger.LogInfo($"[{source}] Learned {learned} Classfixes1 pistol conversion recipe(s) for {itemKey}.");
			}
		}

		private static int LearnMatchingConversionRecipe(object localCharacter, string itemKey, string direction)
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

			HashSet<string> families = new HashSet<string>(GetCandidateFamilies(itemKey));
			if (families.Count == 0)
			{
				return 0;
			}

			int learned = 0;
			foreach (object recipe in allRecipes)
			{
				if (recipe == null)
				{
					continue;
				}

				if (!TryExtractRecipeFamilyAndDirection(recipe, recipeType, out string recipeFamily, out string recipeDirection, out string recipeUid))
				{
					continue;
				}

				if (!string.Equals(recipeDirection, direction, StringComparison.Ordinal) || !families.Contains(recipeFamily))
				{
					continue;
				}

				if (TryLearnRecipe(localCharacter, recipe, recipeType, recipeUid))
				{
					learned++;
				}
			}

			return learned;
		}

		private static bool TryExtractRecipeFamilyAndDirection(object recipe, Type recipeType, out string family, out string direction, out string recipeUid)
		{
			family = string.Empty;
			direction = string.Empty;
			recipeUid = string.Empty;

			List<string> parts = new List<string>
			{
				GetStringMember(recipeType, recipe, "UID"),
				GetStringMember(recipeType, recipe, "RecipeID"),
				GetStringMember(recipeType, recipe, "m_recipeID"),
				GetStringMember(recipeType, recipe, "Name"),
				GetStringMember(recipeType, recipe, "RecipeName"),
				GetStringMember(recipeType, recipe, "m_name"),
				GetStringMember(recipeType, recipe, "m_recipeName")
			};

			if (recipe is UnityEngine.Object unityObject)
			{
				parts.Add(unityObject.name);
			}

			foreach (string text in parts.Where(t => !string.IsNullOrWhiteSpace(t)))
			{
				Match match = Regex.Match(text, "!?(?<family>[^-!]+)-(?<dir>handgunTOpistol|pistolTOhandgun)-custom", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
				if (!match.Success)
				{
					continue;
				}

				family = Normalize(match.Groups["family"].Value);
				direction = Normalize(match.Groups["dir"].Value);
				recipeUid = GetStringMember(recipeType, recipe, "UID")
					?? GetStringMember(recipeType, recipe, "RecipeID")
					?? GetStringMember(recipeType, recipe, "m_recipeID")
					?? text;

				return !string.IsNullOrWhiteSpace(family) && !string.IsNullOrWhiteSpace(direction);
			}

			return false;
		}

		private static bool TryLearnRecipe(object localCharacter, object recipe, Type recipeType, string recipeUid)
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
				.FirstOrDefault(m => string.Equals(m.Name, "LearnRecipe", StringComparison.OrdinalIgnoreCase)
					&& m.GetParameters().Length == 1
					&& m.GetParameters()[0].ParameterType.IsAssignableFrom(recipeType));

			if (learnRecipeMethod == null)
			{
				return false;
			}

			object result = learnRecipeMethod.Invoke(recipeKnowledge, new[] { recipe });
			return result is bool b ? b : true;
		}

		private static string GetConversionDirection(string itemKey)
		{
			if (string.IsNullOrWhiteSpace(itemKey))
			{
				return string.Empty;
			}

			bool hasHandgun = itemKey.Contains("handgun");
			bool hasPistol = itemKey.Contains("pistol");

			if (hasPistol && !hasHandgun)
			{
				return "pistoltohandgun";
			}

			if (hasHandgun && !hasPistol)
			{
				return "handguntopistol";
			}

			return string.Empty;
		}

		private static IEnumerable<string> GetCandidateFamilies(string itemKey)
		{
			if (string.IsNullOrWhiteSpace(itemKey))
			{
				yield break;
			}

			string reduced = itemKey;
			reduced = reduced.Replace("crafting", string.Empty);
			reduced = reduced.Replace("pistol", string.Empty);
			reduced = reduced.Replace("handgun", string.Empty);
			reduced = Normalize(reduced);

			if (!string.IsNullOrWhiteSpace(reduced) && reduced.Length >= 3)
			{
				yield return reduced;
			}

			if (reduced == "handcannon" || reduced == "cannon")
			{
				yield return "cannon";
				yield return "handcannon";
			}
		}

		private static IEnumerable<object> GetEquippedItems(object localCharacter)
		{
			if (localCharacter == null)
			{
				yield break;
			}

			object characterEquipment = GetMemberValue(localCharacter.GetType(), localCharacter, "CharacterEquipment")
				?? GetMemberValue(localCharacter.GetType(), localCharacter, "Equipment")
				?? GetMemberValue(localCharacter.GetType(), localCharacter, "m_equipment");

			if (characterEquipment == null)
			{
				yield break;
			}

			string[] slotCollections = { "m_equipmentSlots", "EquipmentSlots", "m_slots" };
			HashSet<object> seenItems = new HashSet<object>();

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

					if (equippedItem == null || !seenItems.Add(equippedItem))
					{
						continue;
					}

					yield return equippedItem;
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

		private static bool IsClassfixesPart1Installed()
		{
			return AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
			{
				string name = assembly.GetName().Name;
				return name.IndexOf("Classfixes", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("Pistol", StringComparison.OrdinalIgnoreCase) >= 0
					|| name.IndexOf("Handgun", StringComparison.OrdinalIgnoreCase) >= 0;
			});
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
				Instance?.TryUnlockFromEquippedItems("NetworkLevelLoader.UnPauseGameplay");
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
					if (parameters.Length == 2 && parameters[1].ParameterType == typeof(bool))
					{
						yield return method;
					}
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

				Instance.TryUnlockFromEquippedItem(character, __0, "CharacterEquipment.EquipItem");
			}
		}
	}
}