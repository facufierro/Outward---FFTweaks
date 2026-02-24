using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
            new Harmony("fierrof.fft.recipeunlocker").PatchAll();
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

            MethodInfo isLearned = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name.Equals("IsRecipeLearned", StringComparison.OrdinalIgnoreCase)
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(string));

            if (isLearned != null)
            {
                object known = isLearned.Invoke(recipeKnowledge, new object[] { recipeUid });
                if (known is bool b && b)
                {
                    return;
                }
            }

            Type recipeType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(type => type.Name == "Recipe");

            MethodInfo learnRecipeObject = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name.Equals("LearnRecipe", StringComparison.OrdinalIgnoreCase)
                    && m.GetParameters().Length == 1
                    && recipeType != null
                    && m.GetParameters()[0].ParameterType.IsAssignableFrom(recipeType));

            object recipeObject = FindRecipeByUid(recipeUid, recipeType);

            if (learnRecipeObject != null && recipeObject != null)
            {
                learnRecipeObject.Invoke(recipeKnowledge, new[] { recipeObject });
                return;
            }

            MethodInfo learnRecipeString = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name.Equals("LearnRecipe", StringComparison.OrdinalIgnoreCase)
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == typeof(string));

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
                    return null;
                }

                MethodInfo[] equipCandidates = equipmentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method => method.Name == "EquipItem")
                    .ToArray();

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
                    ?? equipCandidates.FirstOrDefault();

                return selected;
            }

            private static void Postfix(object __instance, object __0)
            {
                Instance?.HandleEquip(__instance, __0);
            }
        }
    }
}
