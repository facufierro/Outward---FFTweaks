using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;
using BepInEx;
using Newtonsoft.Json.Linq;

namespace FFT.XMLEditor
{
    [BepInPlugin("fierrof.fft.xmleditor", "FFT.XMLEditor", "1.0.0")]
    public class XMLEditorPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            try
            {
                string pluginDir = Path.GetDirectoryName(Info.Location) ?? string.Empty;
                string dataPath = Path.Combine(pluginDir, "data.json");
                if (!File.Exists(dataPath))
                {
                    Logger.LogWarning($"data.json not found: {dataPath}");
                    return;
                }

                JObject root = JObject.Parse(File.ReadAllText(dataPath));
                Stack<JToken> pending = new Stack<JToken>();
                pending.Push(root);

                while (pending.Count > 0)
                {
                    JToken token = pending.Pop();
                    if (token is JProperty property)
                    {
                        pending.Push(property.Value);
                        continue;
                    }

                    if (token is JArray array)
                    {
                        foreach (JToken child in array)
                        {
                            pending.Push(child);
                        }
                        continue;
                    }

                    JObject node = token as JObject;
                    if (node == null) continue;

                    JArray xmls = node["xmls"] as JArray;
                    if (xmls != null)
                    {
                        foreach (JToken xmlPatchToken in xmls)
                        {
                            JObject xmlPatch = xmlPatchToken as JObject;
                            string targetPathToken = (string)xmlPatch?["targetPath"];
                            if (string.IsNullOrWhiteSpace(targetPathToken)) continue;

                            string target = targetPathToken.Replace('/', Path.DirectorySeparatorChar);
                            string xmlPath = Path.IsPathRooted(target) ? target : Path.Combine(Paths.PluginPath, target);
                            if (!File.Exists(xmlPath)) continue;

                            XDocument xml = XDocument.Load(xmlPath, LoadOptions.PreserveWhitespace);
                            bool changed = false;

                            JArray replacements = xmlPatch["replacements"] as JArray;
                            if (replacements == null) continue;

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

                            if (changed) xml.Save(xmlPath);
                        }
                    }

                    foreach (JProperty childProperty in node.Properties())
                    {
                        pending.Push(childProperty.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }
    }
}
