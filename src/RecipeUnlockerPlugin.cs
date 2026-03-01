using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FFT.RecipeUnlocker
{
    [BepInPlugin("fierrof.fft.recipeunlocker", "FFT.RecipeUnlocker", "1.0.0")]
    public class RecipeUnlockerPlugin : BaseUnityPlugin
    {
        private static RecipeUnlockerPlugin Instance;
        private readonly Dictionary<int, string> recipeByItemId = new Dictionary<int, string>();

        private void Awake()
        {
            Instance = this;
            LoadRecipes();
            PatchEquipItem(new Harmony("fierrof.fft.recipeunlocker"));
        }

        private void PatchEquipItem(Harmony harmony)
        {
            Type equipmentType = AccessTools.TypeByName("CharacterEquipment");
            if (equipmentType == null)
            {
                Logger.LogWarning("CharacterEquipment type not found; equip patch skipped.");
                return;
            }

            var equipMethods = AccessTools.GetDeclaredMethods(equipmentType)
                .Where(method => method.Name == "EquipItem")
                .ToArray();

            var preferred = equipMethods.FirstOrDefault(method =>
            {
                var parameters = method.GetParameters();
                return parameters.Length == 2
                    && parameters[1].ParameterType == typeof(bool)
                    && string.Equals(parameters[0].ParameterType.Name, "Equipment", StringComparison.Ordinal);
            });

            var target = preferred
                ?? equipMethods.FirstOrDefault(method =>
                {
                    var parameters = method.GetParameters();
                    return parameters.Length == 2 && parameters[1].ParameterType == typeof(bool);
                })
                ?? equipMethods.FirstOrDefault();

            if (target == null)
            {
                Logger.LogWarning("EquipItem method not found; equip patch skipped.");
                return;
            }

            harmony.Patch(target, postfix: new HarmonyMethod(AccessTools.Method(typeof(RecipeUnlockerPlugin), nameof(EquipPostfix))));
        }

        private static void EquipPostfix(object __instance, object __0)
        {
            Instance?.HandleEquip(__instance, __0);
        }

        private void LoadRecipes()
        {
            string pluginDir = Path.GetDirectoryName(Info.Location) ?? string.Empty;
            string dataRoot = Path.Combine(pluginDir, "data");
            if (!Directory.Exists(dataRoot))
            {
                Logger.LogWarning($"data folder not found: {dataRoot}");
                return;
            }

            foreach (string jsonPath in Directory.GetFiles(dataRoot, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    JObject root = JObject.Parse(File.ReadAllText(jsonPath));
                    JArray recipes = root["recipes"] as JArray;
                    if (recipes == null) continue;

                    foreach (JToken pairToken in recipes)
                    {
                        JArray pair = pairToken as JArray;
                        if (pair == null || pair.Count < 2) continue;

                        int itemId = ParseInt(pair[0]);
                        string recipeUid = (string)pair[1];
                        if (itemId == int.MinValue || string.IsNullOrWhiteSpace(recipeUid)) continue;

                        recipeByItemId[itemId] = recipeUid;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                }
            }
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

            int itemId = ParseInt(Read(equippedItem, "ItemID", "m_itemID", "ItemId"));
            if (!recipeByItemId.TryGetValue(itemId, out string recipeUid))
            {
                return;
            }

            TryLearnRecipeByUid(character, recipeUid);
        }

        private static void TryLearnRecipeByUid(object character, string recipeUid)
        {
            object inventory = Read(character, "Inventory");
            object recipeKnowledge = Read(inventory, "RecipeKnowledge");
            if (recipeKnowledge == null)
            {
                return;
            }

            var isLearned = AccessTools.Method(recipeKnowledge.GetType(), "IsRecipeLearned", new[] { typeof(string) });

            if (isLearned != null)
            {
                object known = isLearned.Invoke(recipeKnowledge, new object[] { recipeUid });
                if (known is bool b && b)
                {
                    return;
                }
            }

            Type recipeType = AccessTools.TypeByName("Recipe");

            var learnRecipeObject = recipeType != null
                ? AccessTools.Method(recipeKnowledge.GetType(), "LearnRecipe", new[] { recipeType })
                : null;

            object recipeObject = FindRecipeByUid(recipeUid, recipeType);

            if (learnRecipeObject != null && recipeObject != null)
            {
                learnRecipeObject.Invoke(recipeKnowledge, new[] { recipeObject });
                return;
            }

            var learnRecipeString = AccessTools.Method(recipeKnowledge.GetType(), "LearnRecipe", new[] { typeof(string) });

            if (learnRecipeString != null)
            {
                learnRecipeString.Invoke(recipeKnowledge, new object[] { recipeUid });
            }
        }

        private static object FindRecipeByUid(string recipeUid, Type recipeType)
        {
            if (string.IsNullOrWhiteSpace(recipeUid) || recipeType == null)
            {
                return null;
            }

            foreach (object recipe in Resources.FindObjectsOfTypeAll(recipeType))
            {
                string uid = Read(recipe, "UID", "RecipeID", "m_recipeID")?.ToString();
                if (string.Equals(uid, recipeUid, StringComparison.OrdinalIgnoreCase))
                {
                    return recipe;
                }
            }

            return null;
        }

        private static object Read(object instance, params string[] memberNames)
        {
            if (instance == null)
            {
                return null;
            }

            var traverse = Traverse.Create(instance);
            foreach (string memberName in memberNames)
            {
                var field = traverse.Field(memberName);
                if (field.FieldExists())
                {
                    return field.GetValue();
                }

                var property = traverse.Property(memberName);
                if (property.PropertyExists())
                {
                    return property.GetValue();
                }
            }

            return null;
        }

        private static int ParseInt(object value)
        {
            return value != null && int.TryParse(value.ToString(), out int parsed) ? parsed : int.MinValue;
        }

    }
}
