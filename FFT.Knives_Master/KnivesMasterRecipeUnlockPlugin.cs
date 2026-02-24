using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace FFT.Knives_Master
{
    [BepInPlugin("fierrof.fft.knives_master", "FFT.Knives_Master", "1.0.0")]
    public class KnivesMasterRecipeUnlockPlugin : BaseUnityPlugin
    {
        private static KnivesMasterRecipeUnlockPlugin Instance;
        private static readonly Dictionary<int, string> ItemToRecipeUid = new Dictionary<int, string>();

        private void Awake()
        {
            Instance = this;
            new Harmony("fierrof.fft.knives_master").PatchAll();
            BuildItemToRecipeMap();
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

            if (!ItemToRecipeUid.TryGetValue(equippedItemId, out string recipeUid))
            {
                return;
            }

            TryLearnRecipe(character, recipeUid);
        }

        private static void TryLearnRecipe(object character, string recipeUid)
        {
            object inventory = Read(character, "Inventory");
            object recipeKnowledge = Read(inventory, "RecipeKnowledge");
            if (recipeKnowledge == null)
            {
                return;
            }

            MethodInfo isRecipeLearned = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(method => method.Name.Equals("IsRecipeLearned", StringComparison.OrdinalIgnoreCase)
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == typeof(string));

            if (isRecipeLearned != null)
            {
                object known = isRecipeLearned.Invoke(recipeKnowledge, new object[] { recipeUid });
                if (known is bool b && b)
                {
                    return;
                }
            }

            Type recipeType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(type => type.Name == "Recipe");
            if (recipeType == null)
            {
                return;
            }

            object recipeObject = FindLoadedRecipeByUid(recipeUid, recipeType);

            MethodInfo learnRecipeObject = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(method => method.Name.Equals("LearnRecipe", StringComparison.OrdinalIgnoreCase)
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType.IsAssignableFrom(recipeType));

            if (learnRecipeObject != null && recipeObject != null)
            {
                learnRecipeObject.Invoke(recipeKnowledge, new[] { recipeObject });
                return;
            }

            MethodInfo learnRecipeString = recipeKnowledge.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(method => method.Name.Equals("LearnRecipe", StringComparison.OrdinalIgnoreCase)
                    && method.GetParameters().Length == 1
                    && method.GetParameters()[0].ParameterType == typeof(string));

            if (learnRecipeString != null)
            {
                learnRecipeString.Invoke(recipeKnowledge, new object[] { recipeUid });
            }
        }

        private static object FindLoadedRecipeByUid(string recipeUid, Type recipeType)
        {
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

        private static void BuildItemToRecipeMap()
        {
            ItemToRecipeUid.Clear();
            string recipesDir = FindKnivesRecipesDirectory();
            if (string.IsNullOrWhiteSpace(recipesDir) || !Directory.Exists(recipesDir))
            {
                return;
            }

            List<RecipeMeta> recipes = new List<RecipeMeta>();
            foreach (string file in Directory.GetFiles(recipesDir, "*.xml", SearchOption.TopDirectoryOnly))
            {
                RecipeMeta meta = ParseRecipeMeta(file);
                if (meta != null)
                {
                    recipes.Add(meta);
                }
            }

            foreach (var family in recipes
                .Where(r => !string.IsNullOrWhiteSpace(r.Family) && r.ResultItemId != int.MinValue)
                .GroupBy(r => r.Family, StringComparer.OrdinalIgnoreCase))
            {
                RecipeMeta daggerToKnife = family.FirstOrDefault(r => r.Direction == "daggertoknife");
                RecipeMeta knifeToDagger = family.FirstOrDefault(r => r.Direction == "knifetodagger");

                if (daggerToKnife == null || knifeToDagger == null)
                {
                    continue;
                }

                ItemToRecipeUid[knifeToDagger.ResultItemId] = daggerToKnife.Uid;
                ItemToRecipeUid[daggerToKnife.ResultItemId] = knifeToDagger.Uid;
            }
        }

        private static RecipeMeta ParseRecipeMeta(string file)
        {
            try
            {
                XDocument doc = XDocument.Load(file);
                string uid = doc.Root?.Element("UID")?.Value;
                if (string.IsNullOrWhiteSpace(uid))
                {
                    return null;
                }

                string normalizedUid = Normalize(uid);
                string direction = normalizedUid.Contains("daggertoknife") ? "daggertoknife"
                    : normalizedUid.Contains("knifetodagger") ? "knifetodagger"
                    : string.Empty;
                if (string.IsNullOrEmpty(direction))
                {
                    return null;
                }

                string family = ExtractFamily(uid, direction);
                int resultItemId = doc.Root?
                    .Element("Results")?
                    .Elements("ItemQty")?
                    .Select(x => ParseInt((object)x.Element("ItemID")?.Value))
                    .FirstOrDefault(id => id != int.MinValue) ?? int.MinValue;

                return new RecipeMeta
                {
                    Uid = uid,
                    Family = family,
                    Direction = direction,
                    ResultItemId = resultItemId
                };
            }
            catch
            {
                return null;
            }
        }

        private static string FindKnivesRecipesDirectory()
        {
            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            string pluginsRoot = Directory.GetParent(pluginDir)?.FullName ?? pluginDir;

            string[] candidates =
            {
                Path.Combine(pluginsRoot, "stormcancer-Knives_Master", "SideLoader", "Recipes"),
                Path.Combine(pluginsRoot, "fierrof-FFTweaks", "stormcancer-Knives_Master", "SideLoader", "Recipes")
            };

            return candidates.FirstOrDefault(Directory.Exists);
        }

        private static string ExtractFamily(string uid, string direction)
        {
            string pattern = direction == "daggertoknife" ? "(?<family>.+)-daggerTOknife" : "(?<family>.+)-knifeTOdagger";
            Match match = Regex.Match(uid, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return string.Empty;
            }

            return Normalize(match.Groups["family"].Value.Trim().TrimStart('!'));
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

        private static string Normalize(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        private sealed class RecipeMeta
        {
            public string Uid;
            public string Family;
            public string Direction;
            public int ResultItemId;
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

                MethodInfo selected = equipCandidates.FirstOrDefault(method =>
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 2
                        && parameters[1].ParameterType == typeof(bool)
                        && string.Equals(parameters[0].ParameterType.Name, "Equipment", StringComparison.Ordinal);
                })
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
