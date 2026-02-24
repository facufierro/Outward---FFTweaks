using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;

namespace FFT.MoreDecraftingRecipes
{
	[BepInPlugin("fierrof.fft.moredecraftingrecipes", "FFT.MoreDecraftingRecipes", "1.0.0")]
	public class MoreDecraftingRecipesRecipeUnlockPlugin : BaseUnityPlugin
	{
		private static readonly bool DebugEnabled = true;
		private static MoreDecraftingRecipesRecipeUnlockPlugin Instance;
		private static bool _loggedRecipeKnowledgeMethods;

		private static readonly Dictionary<int, string> ArrowRecipeByItemId = new Dictionary<int, string>
		{
			{ 5200001, "madhoek.Arrows_disassemble" },
			{ 5200002, "madhoek.FlamingArrow_disassemble" },
			{ 5200003, "madhoek.PoisonArrow_disassemble" },
			{ 5200004, "madhoek.VenomArrow_disassemble" },
			{ 5200005, "madhoek.PalladiumArrow_disassemble" },
			{ 5200007, "madhoek.ExplosiveArrow_disassemble" },
			{ 5200008, "madhoek.Arrow_disassemble" },
			{ 5200009, "madhoek.HolyRageArrow_disassemble" },
			{ 5200010, "madhoek.SoulRuptureArrow_disassemble" },
			{ 5200019, "madhoek.ManaArrow_disassemble" }
		};

		private void Awake()
		{
			Instance = this;
			new Harmony("fierrof.fft.moredecraftingrecipes").PatchAll();
			DebugLog("Plugin awake and patches applied.");
		}

		private void HandleEquip(object characterEquipment, object equippedItem)
		{
			DebugLog($"HandleEquip fired. equipmentType={characterEquipment?.GetType().FullName ?? "null"}, itemType={equippedItem?.GetType().FullName ?? "null"}");

			if (characterEquipment == null || equippedItem == null)
			{
				DebugLog("Stop: characterEquipment or equippedItem is null.");
				return;
			}

			object character = Read(characterEquipment, "m_character", "Character", "OwnerCharacter");
			DebugLog($"Resolved character type={character?.GetType().FullName ?? "null"}");
			if (!(Read(character, "IsLocalPlayer") is bool isLocalPlayer) || !isLocalPlayer)
			{
				DebugLog("Stop: character is not local player.");
				return;
			}

			int itemId = ParseInt(Read(equippedItem, "ItemID", "m_itemID", "ItemId"));
			DebugLog($"Equipped item id={itemId}");
			if (!ArrowRecipeByItemId.TryGetValue(itemId, out string recipeUid))
			{
				DebugLog("Stop: equipped item id not in ArrowRecipeByItemId map.");
				return;
			}

			DebugLog($"Mapped item {itemId} to recipe uid '{recipeUid}'");
			DebugLog($"Recipe uid exists in loaded recipes: {LoadedRecipesContainUid(recipeUid)}");

			TryLearnRecipeByUid(character, recipeUid);
		}

		private static void TryLearnRecipeByUid(object character, string recipeUid)
		{
			object inventory = Read(character, "Inventory");
			DebugLog($"Inventory type={inventory?.GetType().FullName ?? "null"}");
			object recipeKnowledge = Read(inventory, "RecipeKnowledge");
			if (recipeKnowledge == null)
			{
				DebugLog("Stop: recipeKnowledge is null.");
				return;
			}

			LogRecipeKnowledgeMethodsOnce(recipeKnowledge);

			MethodInfo isLearned = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.FirstOrDefault(m => m.Name.Equals("IsRecipeLearned", StringComparison.OrdinalIgnoreCase)
					&& m.GetParameters().Length == 1
					&& m.GetParameters()[0].ParameterType == typeof(string));
			DebugLog($"Method IsRecipeLearned(string) found: {isLearned != null}");
			if (isLearned != null)
			{
				try
				{
					object known = isLearned.Invoke(recipeKnowledge, new object[] { recipeUid });
					DebugLog($"IsRecipeLearned('{recipeUid}') => {known}");
					if (known is bool b && b)
					{
						DebugLog("Stop: recipe already learned.");
						return;
					}
				}
				catch (Exception ex)
				{
					DebugLog($"IsRecipeLearned invoke failed: {ex.GetType().Name}: {ex.Message}");
				}
			}

			MethodInfo learnRecipe = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.FirstOrDefault(m => m.Name.Equals("LearnRecipe", StringComparison.OrdinalIgnoreCase)
					&& m.GetParameters().Length == 1
					&& m.GetParameters()[0].ParameterType == typeof(string));
			DebugLog($"Method LearnRecipe(string) found: {learnRecipe != null}");

			Type recipeType = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(assembly => assembly.GetTypes())
				.FirstOrDefault(type => type.Name == "Recipe");

			MethodInfo learnRecipeObject = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.FirstOrDefault(m => m.Name.Equals("LearnRecipe", StringComparison.OrdinalIgnoreCase)
					&& m.GetParameters().Length == 1
					&& recipeType != null
					&& m.GetParameters()[0].ParameterType.IsAssignableFrom(recipeType));
			DebugLog($"Method LearnRecipe(Recipe) found: {learnRecipeObject != null}");

			object recipeObject = FindRecipeByUid(recipeUid, recipeType);
			DebugLog($"Recipe object resolved for uid '{recipeUid}': {recipeObject != null}");

			if (learnRecipeObject != null && recipeObject != null)
			{
				try
				{
					object result = learnRecipeObject.Invoke(recipeKnowledge, new[] { recipeObject });
					DebugLog($"LearnRecipe(Recipe '{recipeUid}') invoked. Result={result ?? "<null>"}");
				}
				catch (Exception ex)
				{
					DebugLog($"LearnRecipe(Recipe) invoke failed: {ex.GetType().Name}: {ex.Message}");
				}
				return;
			}

			if (learnRecipe != null)
			{
				try
				{
					object result = learnRecipe.Invoke(recipeKnowledge, new object[] { recipeUid });
					DebugLog($"LearnRecipe('{recipeUid}') invoked. Result={result ?? "<null>"}");
				}
				catch (Exception ex)
				{
					DebugLog($"LearnRecipe invoke failed: {ex.GetType().Name}: {ex.Message}");
				}
			}
			else
			{
				DebugLog("Stop: no usable LearnRecipe overload found.");
			}
		}

		private static object FindRecipeByUid(string recipeUid, Type recipeType)
		{
			if (string.IsNullOrWhiteSpace(recipeUid) || recipeType == null)
			{
				return null;
			}

			foreach (object recipe in UnityEngine.Resources.FindObjectsOfTypeAll(recipeType))
			{
				string uid = Read(recipe, "UID", "RecipeID", "m_recipeID")?.ToString();
				if (string.Equals(uid, recipeUid, StringComparison.OrdinalIgnoreCase))
				{
					return recipe;
				}
			}

			return null;
		}

		private static bool LoadedRecipesContainUid(string recipeUid)
		{
			if (string.IsNullOrWhiteSpace(recipeUid))
			{
				return false;
			}

			Type recipeType = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(assembly => assembly.GetTypes())
				.FirstOrDefault(type => type.Name == "Recipe");

			if (recipeType == null)
			{
				return false;
			}

			foreach (object recipe in HarmonyLib.AccessTools.TypeByName("UnityEngine.Resources") == null ? new object[0] : UnityEngine.Resources.FindObjectsOfTypeAll(recipeType))
			{
				string uid = Read(recipe, "UID", "RecipeID", "m_recipeID")?.ToString();
				if (string.Equals(uid, recipeUid, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		private static void LogRecipeKnowledgeMethodsOnce(object recipeKnowledge)
		{
			if (!DebugEnabled || _loggedRecipeKnowledgeMethods || recipeKnowledge == null)
			{
				return;
			}

			_loggedRecipeKnowledgeMethods = true;
			MethodInfo[] methods = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.Where(m => string.Equals(m.Name, "LearnRecipe", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(m.Name, "IsRecipeLearned", StringComparison.OrdinalIgnoreCase))
				.ToArray();

			if (methods.Length == 0)
			{
				DebugLog("RecipeKnowledge methods found: none for LearnRecipe/IsRecipeLearned.");
				return;
			}

			foreach (MethodInfo method in methods)
			{
				string args = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.FullName));
				DebugLog($"RecipeKnowledge method: {method.Name}({args})");
			}
		}

		private static void DebugLog(string message)
		{
			if (!DebugEnabled)
			{
				return;
			}

			Instance?.Logger.LogInfo($"[FFT.MoreDecraftingRecipes][DEBUG] {message}");
		}

		private static object Read(object instance, params string[] memberNames)
		{
			if (instance == null)
			{
				return null;
			}

			Type type = instance.GetType();
			foreach (string memberName in memberNames)
			{
				PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
				if (property != null)
				{
					return property.GetValue(instance);
				}

				FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
				if (field != null)
				{
					return field.GetValue(instance);
				}
			}

			return null;
		}

		private static int ParseInt(object value)
		{
			return value != null && int.TryParse(value.ToString(), out int parsed) ? parsed : int.MinValue;
		}

		[HarmonyPatch]
		private static class EquipPatch
		{
			private static MethodBase TargetMethod()
			{
				Type equipmentType = AccessTools.TypeByName("CharacterEquipment");
				if (equipmentType == null)
				{
					DebugLog("EquipPatch.TargetMethod: CharacterEquipment type not found.");
					return null;
				}

				MethodInfo[] equipCandidates = equipmentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
					.Where(method => method.Name == "EquipItem")
					.ToArray();

				if (equipCandidates.Length == 0)
				{
					DebugLog("EquipPatch.TargetMethod: no EquipItem methods found.");
					return null;
				}

				foreach (MethodInfo candidate in equipCandidates)
				{
					string signature = string.Join(", ", candidate.GetParameters().Select(p => p.ParameterType.FullName));
					DebugLog($"EquipPatch candidate: EquipItem({signature})");
				}

				MethodInfo preferred = equipCandidates.FirstOrDefault(method =>
				{
					ParameterInfo[] parameters = method.GetParameters();
					return parameters.Length == 2
						&& parameters[1].ParameterType == typeof(bool)
						&& string.Equals(parameters[0].ParameterType.Name, "Equipment", StringComparison.Ordinal);
				});

				MethodInfo selected = preferred
					?? equipCandidates.FirstOrDefault(method =>
					{
						ParameterInfo[] parameters = method.GetParameters();
						return parameters.Length == 2 && parameters[1].ParameterType == typeof(bool);
					})
					?? equipCandidates.First();
				string selectedSignature = string.Join(", ", selected.GetParameters().Select(p => p.ParameterType.FullName));
				DebugLog($"EquipPatch selected: EquipItem({selectedSignature})");
				return selected;
			}

			private static void Postfix(object __instance, object __0)
			{
				DebugLog($"EquipPatch.Postfix fired. __instanceType={__instance?.GetType().FullName ?? "null"}, __0Type={__0?.GetType().FullName ?? "null"}");
				Instance?.HandleEquip(__instance, __0);
			}
		}
	}
}
