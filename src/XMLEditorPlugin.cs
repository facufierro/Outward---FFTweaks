using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BepInEx;
using FFT.Config;
using Newtonsoft.Json.Linq;

namespace FFT.XMLEditor
{
    [BepInPlugin("fierrof.fft.xmleditor", "FFT.XMLEditor", "1.0.0")]
    public class XMLEditorPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            ReplacementControl.Initialize(Info.Location, Logger);
            ReplacementControl.RefreshRequested += Run;
            Run();
        }

        private void Run()
        {
            try
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
                    JObject root = JObject.Parse(File.ReadAllText(jsonPath));
                    JArray xmls = root["xmls"] as JArray;
                    if (xmls == null) continue;

                    foreach (JToken xmlPatchToken in xmls)
                    {
                        JObject xmlPatch = xmlPatchToken as JObject;
                        bool firstInstallOnly = ReplacementControl.IsFlagEnabled(xmlPatch?["once"]);

                        if (!ReplacementControl.ShouldApplyFlaggedReplacement(firstInstallOnly)) continue;

                        string targetPathToken = (string)xmlPatch?["targetPath"];
                        if (string.IsNullOrWhiteSpace(targetPathToken)) continue;

                        string target = targetPathToken.Replace('/', Path.DirectorySeparatorChar);
                        string xmlPath = ResolveTargetPath(target);
                        if (!File.Exists(xmlPath)) continue;

                        XDocument xml = XDocument.Load(xmlPath, LoadOptions.PreserveWhitespace);
                        bool changed = false;

                        JArray replacements = xmlPatch["replacements"] as JArray;
                        if (replacements != null)
                        {
                            foreach (JToken replacementToken in replacements)
                            {
                                JArray replacement = replacementToken as JArray;
                                if (replacement == null || replacement.Count < 2) continue;
                                string key = (string)replacement[0];
                                string value = (string)replacement[1];
                                if (string.IsNullOrWhiteSpace(key) || value == null) continue;

                                foreach (XElement element in xml.Descendants())
                                {
                                    bool keyMatches =
                                        string.Equals((string)element.Attribute("key"), key, StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals((string)element.Attribute("name"), key, StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(element.Name.LocalName, key, StringComparison.OrdinalIgnoreCase);
                                    if (!keyMatches) continue;

                                    XAttribute valueAttribute = element.Attribute("value");
                                    if (valueAttribute != null)
                                    {
                                        valueAttribute.Value = value;
                                        changed = true;
                                        continue;
                                    }

                                    bool vectorChanged = false;
                                    foreach (string part in value.Split(','))
                                    {
                                        string[] pair = part.Split('=');
                                        if (pair.Length != 2) continue;
                                        XElement child = element.Element(pair[0].Trim());
                                        if (child == null) continue;
                                        child.Value = pair[1].Trim();
                                        vectorChanged = true;
                                        changed = true;
                                    }
                                    if (vectorChanged) continue;

                                    if (!element.HasElements)
                                    {
                                        element.Value = value;
                                        changed = true;
                                    }
                                }
                            }
                        }

                        JArray additions = xmlPatch["additions"] as JArray;
                        if (additions != null)
                        {
                            foreach (JToken additionToken in additions)
                            {
                                JArray addition = additionToken as JArray;
                                if (addition == null || addition.Count < 2) continue;
                                string parentName = (string)addition[0];
                                string rawXml = (string)addition[1];
                                if (string.IsNullOrWhiteSpace(parentName) || string.IsNullOrWhiteSpace(rawXml)) continue;

                                XElement parsed = XElement.Parse(rawXml);
                                foreach (XElement parent in xml.Descendants()
                                    .Where(e => string.Equals(e.Name.LocalName, parentName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    bool alreadyExists = parent.Elements()
                                        .Any(e => XNode.DeepEquals(e, parsed));
                                    if (alreadyExists) continue;

                                    parent.Add(parsed);
                                    changed = true;
                                }
                            }
                        }

                        if (changed) xml.Save(xmlPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private static string ResolveTargetPath(string target)
        {
            if (Path.IsPathRooted(target))
            {
                return target;
            }

            string normalized = target.Replace('/', Path.DirectorySeparatorChar);
            string bepInExRoot = Directory.GetParent(Paths.PluginPath)?.FullName ?? Paths.PluginPath;

            if (normalized.StartsWith("plugins" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(bepInExRoot, normalized);
            }

            return Path.Combine(bepInExRoot, "plugins", normalized);
        }
    }
}
