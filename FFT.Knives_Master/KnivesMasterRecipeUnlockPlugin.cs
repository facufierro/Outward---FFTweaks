using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace FFT.Knives_Master
{
    [BepInPlugin("fierrof.fft.knives_master", "FFT.Knives_Master", "1.0.0")]
    public class KnivesMasterRecipeUnlockPlugin : BaseUnityPlugin
    {
        private static KnivesMasterRecipeUnlockPlugin Instance;

        private void Awake()
        {
            Instance = this;
            new Harmony("fierrof.fft.knives_master").PatchAll();
        }

        private void HandleEquip(object characterEquipment, object equippedItem)
        {
            if (characterEquipment == null || equippedItem == null)
            {
                return;
            }

            object character = Read(characterEquipment, "m_character", "Character", "OwnerCharacter");
            if (!(Read(character, "IsLocalPlayer") is bool isLocalPlayer) || !isLocalPlayer)
            {
                return;
            }

            int equippedItemId = ParseInt(Read(equippedItem, "ItemID", "m_itemID", "ItemId"));
            if (equippedItemId == int.MinValue)
            {
                return;
            }

            object inventory = Read(character, "Inventory");
            object recipeKnowledge = Read(inventory, "RecipeKnowledge");
            if (recipeKnowledge == null)
            {
                return;
            }

            Type recipeType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(type => type.Name == "Recipe");
            if (recipeType == null)
            {
                return;
            }

            MethodInfo isRecipeLearned = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(method => method.Name.Equals("IsRecipeLearned", StringComparison.OrdinalIgnoreCase)
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == typeof(string));

            MethodInfo learnRecipe = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(method => method.Name.Equals("LearnRecipe", StringComparison.OrdinalIgnoreCase)
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == typeof(string));
            if (learnRecipe == null)
            {
                return;
            }

            foreach (object recipe in Resources.FindObjectsOfTypeAll(recipeType))
            {
                string uid = Read(recipe, "UID", "RecipeID", "m_recipeID")?.ToString();
                if (string.IsNullOrWhiteSpace(uid))
                {
                    continue;
                }

                string normalizedUid = new string(uid.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
                if (!normalizedUid.Contains("daggertoknife") && !normalizedUid.Contains("knifetodagger"))
                {
                    continue;
                }

                IEnumerable ingredients = Read(recipe, "Ingredients", "m_ingredients", "IngredientStacks", "m_ingredientStacks") as IEnumerable;
                if (ingredients == null)
                {
                    continue;
                }

                int sourceItemId = int.MinValue;
                foreach (object ingredient in ingredients)
                {
                    sourceItemId = ParseInt(Read(ingredient, "SelectorValue", "m_selectorValue", "selectorValue", "ItemID", "m_itemID", "ItemId", "Ingredient_ItemID"));
                    if (sourceItemId != int.MinValue)
                    {
                        break;
                    }
                }

                if (sourceItemId != equippedItemId)
                {
                    continue;
                }

                if (isRecipeLearned != null)
                {
                    object known = isRecipeLearned.Invoke(recipeKnowledge, new object[] { uid });
                    if (known is bool knownBool && knownBool)
                    {
                        continue;
                    }
                }

                learnRecipe.Invoke(recipeKnowledge, new object[] { uid });
            }
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
                return equipmentType?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(method => method.Name == "EquipItem"
                        && method.GetParameters().Length == 2
                        && method.GetParameters()[1].ParameterType == typeof(bool));
            }

            private static void Postfix(object __instance, object __0)
            {
                Instance?.HandleEquip(__instance, __0);
            }
        }
    }
}
