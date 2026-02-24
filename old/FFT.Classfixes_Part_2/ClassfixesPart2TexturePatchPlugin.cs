using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;
using BepInEx;

namespace FFT.Classfixes_Part_2
{
    [BepInPlugin("fierrof.fft.classfixes_part_2", "FFT.Classfixes_Part_2", "1.0.0")]
    public class ClassfixesPart2TexturePatchPlugin : BaseUnityPlugin
    {
        private static readonly List<(string SourceRelativePath, string TargetRelativePath)> Replacements = new()
        {
            // Rune Wu icon replacement
            (
                @"overrides\Rune_Wu\icon.png",
                @"stormcancer-Classfixes_Part_2\SideLoader\Items\-23285_Rune_ Wu\Textures\icon.png"
            ),
            (
                @"overrides\Rune_Wu\skillicon.png",
                @"stormcancer-Classfixes_Part_2\SideLoader\Items\-23285_Rune_ Wu\Textures\skillicon.png"
            ),
            // Runic Spirit Sword Textures
            (
                @"stormcancer-Classfixes_Part_2\SideLoader\Items\-35248_Runic Spirit Sword\Textures\mat_itm_blackSteelWeapons\_AlphaTex.png",
                @"stormcancer-Classfixes_Part_2\SideLoader\Items\-35248_Runic Spirit Sword\Textures\mat_itm_longsword\_AlphaTex.png"
            )
        };

        private static readonly (string FilePath, List<(string Key, string Value)> Replacements) XmlPatch =
        (
            @"stormcancer-Classfixes_Part_2\SideLoader\Items\-35248_Runic Spirit Sword\-35248_Runic Spirit Sword.xml",
            new List<(string Key, string Value)>
            {
                (
                    "key",
                    "value"
                ),
                (
                    "key",
                    "value"
                ),
                (
                    "key",
                    "value"
                )
            }
        );

        private void Awake()
        {
            ReplaceFiles();
            ReplaceXmlValues();
        }

        private void ReplaceFiles()
        {
            try
            {
                string pluginDir = Path.GetDirectoryName(Info.Location) ?? string.Empty;

                foreach ((string sourceRelativePath, string targetRelativePath) in Replacements)
                {
                    string sourcePath = Path.Combine(pluginDir, sourceRelativePath);
                    string targetPath = Path.Combine(Paths.PluginPath, targetRelativePath);

                    if (!File.Exists(sourcePath))
                    {
                        continue;
                    }

                    string targetDir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrWhiteSpace(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    File.Copy(sourcePath, targetPath, true);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private void ReplaceXmlValues()
        {
            try
            {
                string xmlPath = Path.IsPathRooted(XmlPatch.FilePath)
                    ? XmlPatch.FilePath
                    : Path.Combine(Paths.PluginPath, XmlPatch.FilePath);

                if (!File.Exists(xmlPath))
                {
                    return;
                }

                XDocument xml = XDocument.Load(xmlPath, LoadOptions.PreserveWhitespace);
                bool wasUpdated = false;

                foreach ((string key, string value) in XmlPatch.Replacements)
                {
                    foreach (XElement element in xml.Descendants())
                    {
                        bool keyMatches =
                            string.Equals((string)element.Attribute("key"), key, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals((string)element.Attribute("name"), key, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(element.Name.LocalName, key, StringComparison.OrdinalIgnoreCase);

                        if (!keyMatches)
                        {
                            continue;
                        }

                        XAttribute valueAttribute = element.Attribute("value");
                        if (valueAttribute != null)
                        {
                            valueAttribute.Value = value;
                            wasUpdated = true;
                            continue;
                        }

                        if (!element.HasElements)
                        {
                            element.Value = value;
                            wasUpdated = true;
                        }
                    }
                }

                if (wasUpdated)
                {
                    xml.Save(xmlPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }
    }
}
