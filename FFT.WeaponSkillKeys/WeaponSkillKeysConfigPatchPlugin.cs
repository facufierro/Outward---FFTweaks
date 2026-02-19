using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace FFT.WeaponSkillKeys
{
    [BepInDependency("faeryn.weaponskillkeys", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class WeaponSkillKeysConfigPatchPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "fierrof.fft.weaponskillkeys";
        private const string PluginName = "FFT.WeaponSkillKeys";
        private const string PluginVersion = "1.0.0";

        internal static WeaponSkillKeysConfigPatchPlugin Instance;
        internal static ManualLogSourceProxy LogProxy;

        private readonly object _rulesLock = new object();
        private List<WeaponSkillRule> _rules = new List<WeaponSkillRule>();

        private string _rulesPath;
        private DateTime _lastWriteUtc;
        private float _nextReloadCheckTime;

        private ConfigEntry<bool> _autoReload;
        private ConfigEntry<bool> _verboseLogging;

        private void Awake()
        {
            Instance = this;
            LogProxy = new ManualLogSourceProxy(Logger);

            _autoReload = Config.Bind("General", "AutoReloadRules", true, "Reload rules file automatically when it changes.");
            _verboseLogging = Config.Bind("General", "VerboseLogging", false, "Log matching details for debugging.");

            _rulesPath = Path.Combine(Paths.ConfigPath, "fierrof.fft.weaponskillkeys.rules.json");
            EnsureRulesFileExists();
            ReloadRules(forceLog: true);

            new Harmony(PluginGuid).PatchAll();
            Logger.LogInfo($"{PluginName} loaded. Rules: {_rules.Count}");
        }

        private void Update()
        {
            if (!_autoReload.Value)
            {
                return;
            }

            if (Time.unscaledTime < _nextReloadCheckTime)
            {
                return;
            }

            _nextReloadCheckTime = Time.unscaledTime + 2f;
            ReloadRulesIfChanged();
        }

        internal bool TryApplyRule(Equipment equipment, Equipment otherEquipment, ref bool result, ref int id)
        {
            if (equipment == null)
            {
                return false;
            }

            List<WeaponSkillRule> snapshot;
            lock (_rulesLock)
            {
                snapshot = _rules;
            }

            if (snapshot == null || snapshot.Count == 0)
            {
                return false;
            }

            ItemView main = ItemView.From(equipment);
            ItemView other = ItemView.From(otherEquipment);

            foreach (WeaponSkillRule rule in snapshot)
            {
                if (!rule.Enabled)
                {
                    continue;
                }

                if (!RuleMatches(rule, main, other))
                {
                    continue;
                }

                if (string.Equals(rule.Action, "disable", StringComparison.OrdinalIgnoreCase))
                {
                    result = false;
                    id = 0;
                    LogMatch(rule, main, other, "disable");
                    return true;
                }

                if (rule.SkillId.HasValue)
                {
                    result = true;
                    id = rule.SkillId.Value;
                    LogMatch(rule, main, other, "set");
                    return true;
                }
            }

            return false;
        }

        private void LogMatch(WeaponSkillRule rule, ItemView main, ItemView other, string action)
        {
            if (!_verboseLogging.Value)
            {
                return;
            }

            Logger.LogInfo($"Rule matched ({action}): {rule.Id ?? "<no-id>"} | item={main.DebugName} ({main.ItemId}) | other={other?.DebugName ?? "<none>"}");
        }

        private static bool RuleMatches(WeaponSkillRule rule, ItemView item, ItemView other)
        {
            if (!ItemMatches(rule, item, isOther: false))
            {
                return false;
            }

            bool hasOtherConstraint =
                rule.OtherItemId.HasValue ||
                rule.OtherWeaponType.HasValue ||
                rule.OtherIkMode.HasValue ||
                !string.IsNullOrWhiteSpace(rule.OtherItemUidContains) ||
                !string.IsNullOrWhiteSpace(rule.OtherItemNameContains) ||
                (rule.OtherRequiredTags != null && rule.OtherRequiredTags.Count > 0);

            if (!hasOtherConstraint)
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            return ItemMatches(rule, other, isOther: true);
        }

        private static bool ItemMatches(WeaponSkillRule rule, ItemView item, bool isOther)
        {
            if (item == null)
            {
                return false;
            }

            int? itemId = isOther ? rule.OtherItemId : rule.ItemId;
            int? weaponType = isOther ? rule.OtherWeaponType : rule.WeaponType;
            int? ikMode = isOther ? rule.OtherIkMode : rule.IkMode;
            string uidContains = isOther ? rule.OtherItemUidContains : rule.ItemUidContains;
            string nameContains = isOther ? rule.OtherItemNameContains : rule.ItemNameContains;
            List<string> requiredTags = isOther ? rule.OtherRequiredTags : rule.RequiredTags;

            if (itemId.HasValue && item.ItemId != itemId.Value)
            {
                return false;
            }

            if (weaponType.HasValue && item.WeaponType != weaponType.Value)
            {
                return false;
            }

            if (ikMode.HasValue && item.IkMode != ikMode.Value)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(uidContains) &&
                (string.IsNullOrWhiteSpace(item.Uid) || item.Uid.IndexOf(uidContains, StringComparison.OrdinalIgnoreCase) < 0))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(nameContains) &&
                (string.IsNullOrWhiteSpace(item.DebugName) || item.DebugName.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0))
            {
                return false;
            }

            if (requiredTags != null && requiredTags.Count > 0)
            {
                HashSet<string> tags = item.Tags;
                foreach (string requiredTag in requiredTags)
                {
                    if (string.IsNullOrWhiteSpace(requiredTag))
                    {
                        continue;
                    }

                    if (!tags.Contains(requiredTag))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private void EnsureRulesFileExists()
        {
            if (File.Exists(_rulesPath))
            {
                _lastWriteUtc = File.GetLastWriteTimeUtc(_rulesPath);
                return;
            }

            RuleSet defaultRules = new RuleSet
            {
                Rules = new List<WeaponSkillRule>
                {
                    new WeaponSkillRule
                    {
                        Id = "example-disable-pistols",
                        Enabled = false,
                        Priority = 200,
                        Action = "disable",
                        WeaponType = 45
                    },
                    new WeaponSkillRule
                    {
                        Id = "example-specific-item",
                        Enabled = false,
                        Priority = 100,
                        Action = "set",
                        ItemId = 123456,
                        SkillId = 8100310
                    }
                }
            };

            string json = Serialize(defaultRules);
            File.WriteAllText(_rulesPath, json);
            _lastWriteUtc = File.GetLastWriteTimeUtc(_rulesPath);
            Logger.LogInfo($"Created default rules file at {_rulesPath}");
        }

        private void ReloadRulesIfChanged()
        {
            if (!File.Exists(_rulesPath))
            {
                return;
            }

            DateTime writeUtc = File.GetLastWriteTimeUtc(_rulesPath);
            if (writeUtc <= _lastWriteUtc)
            {
                return;
            }

            ReloadRules(forceLog: true);
        }

        private void ReloadRules(bool forceLog)
        {
            try
            {
                string json = File.ReadAllText(_rulesPath);
                RuleSet data = Deserialize(json) ?? new RuleSet();

                List<WeaponSkillRule> loaded = (data.Rules ?? new List<WeaponSkillRule>())
                    .Where(r => r != null)
                    .OrderBy(r => r.Priority)
                    .ToList();

                lock (_rulesLock)
                {
                    _rules = loaded;
                }

                _lastWriteUtc = File.GetLastWriteTimeUtc(_rulesPath);

                if (forceLog)
                {
                    Logger.LogInfo($"Loaded {loaded.Count} weapon-skill rule(s) from {_rulesPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to reload rules from {_rulesPath}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static RuleSet Deserialize(string json)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(RuleSet));
            using (MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            {
                return serializer.ReadObject(stream) as RuleSet;
            }
        }

        private static string Serialize(RuleSet data)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(RuleSet));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, data);
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }

    [HarmonyPatch]
    public static class EquipmentExtensionsSinglePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method("WeaponSkillKeys.Extensions.EquipmentExtensions:TryGetSkillID", new[]
            {
                typeof(Equipment),
                typeof(int).MakeByRefType()
            });
        }

        private static void Postfix(Equipment equipment, ref int id, ref bool __result)
        {
            WeaponSkillKeysConfigPatchPlugin.Instance?.TryApplyRule(equipment, null, ref __result, ref id);
        }
    }

    [HarmonyPatch]
    public static class EquipmentExtensionsDualPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method("WeaponSkillKeys.Extensions.EquipmentExtensions:TryGetSkillID", new[]
            {
                typeof(Equipment),
                typeof(Equipment),
                typeof(int).MakeByRefType()
            });
        }

        private static void Postfix(Equipment equipment, Equipment otherEquipment, ref int id, ref bool __result)
        {
            WeaponSkillKeysConfigPatchPlugin.Instance?.TryApplyRule(equipment, otherEquipment, ref __result, ref id);
        }
    }

    [DataContract]
    public class RuleSet
    {
        [DataMember(Name = "rules")]
        public List<WeaponSkillRule> Rules { get; set; } = new List<WeaponSkillRule>();
    }

    [DataContract]
    public class WeaponSkillRule
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "enabled")]
        public bool Enabled { get; set; } = true;

        [DataMember(Name = "priority")]
        public int Priority { get; set; } = 100;

        [DataMember(Name = "action")]
        public string Action { get; set; } = "set";

        [DataMember(Name = "skillId")]
        public int? SkillId { get; set; }

        [DataMember(Name = "itemId")]
        public int? ItemId { get; set; }

        [DataMember(Name = "itemUidContains")]
        public string ItemUidContains { get; set; }

        [DataMember(Name = "itemNameContains")]
        public string ItemNameContains { get; set; }

        [DataMember(Name = "weaponType")]
        public int? WeaponType { get; set; }

        [DataMember(Name = "ikMode")]
        public int? IkMode { get; set; }

        [DataMember(Name = "requiredTags")]
        public List<string> RequiredTags { get; set; } = new List<string>();

        [DataMember(Name = "otherItemId")]
        public int? OtherItemId { get; set; }

        [DataMember(Name = "otherItemUidContains")]
        public string OtherItemUidContains { get; set; }

        [DataMember(Name = "otherItemNameContains")]
        public string OtherItemNameContains { get; set; }

        [DataMember(Name = "otherWeaponType")]
        public int? OtherWeaponType { get; set; }

        [DataMember(Name = "otherIkMode")]
        public int? OtherIkMode { get; set; }

        [DataMember(Name = "otherRequiredTags")]
        public List<string> OtherRequiredTags { get; set; } = new List<string>();
    }

    internal sealed class ItemView
    {
        public int ItemId { get; private set; }
        public string Uid { get; private set; }
        public string DebugName { get; private set; }
        public int WeaponType { get; private set; }
        public int IkMode { get; private set; }
        public HashSet<string> Tags { get; private set; }

        public static ItemView From(Item item)
        {
            if (item == null)
            {
                return null;
            }

            Equipment equipment = item as Equipment;
            Weapon weapon = item as Weapon;

            HashSet<string> tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (item.Tags != null)
            {
                foreach (Tag tag in item.Tags)
                {
                    if (tag != null && !string.IsNullOrWhiteSpace(tag.TagName))
                    {
                        tags.Add(tag.TagName);
                    }
                }
            }

            return new ItemView
            {
                ItemId = item.ItemID,
                Uid = ReadString(item, "UID", "m_uid", "ItemUID"),
                DebugName = item.Name,
                WeaponType = weapon != null ? (int)weapon.Type : int.MinValue,
                IkMode = equipment != null ? (int)equipment.IKType : int.MinValue,
                Tags = tags
            };
        }

        private static string ReadString(object instance, params string[] names)
        {
            if (instance == null)
            {
                return string.Empty;
            }

            Type type = instance.GetType();
            foreach (string name in names)
            {
                PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (property != null && property.PropertyType == typeof(string))
                {
                    object value = property.GetValue(instance, null);
                    if (value is string s && !string.IsNullOrWhiteSpace(s))
                    {
                        return s;
                    }
                }

                FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (field != null && field.FieldType == typeof(string))
                {
                    object value = field.GetValue(instance);
                    if (value is string s2 && !string.IsNullOrWhiteSpace(s2))
                    {
                        return s2;
                    }
                }
            }

            return string.Empty;
        }
    }

    internal sealed class ManualLogSourceProxy
    {
        private readonly BepInEx.Logging.ManualLogSource _source;

        public ManualLogSourceProxy(BepInEx.Logging.ManualLogSource source)
        {
            _source = source;
        }

        public void LogInfo(string message) => _source.LogInfo(message);
        public void LogWarning(string message) => _source.LogWarning(message);
        public void LogError(string message) => _source.LogError(message);
    }
}
