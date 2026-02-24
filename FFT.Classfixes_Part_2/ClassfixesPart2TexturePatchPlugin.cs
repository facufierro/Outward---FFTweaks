using System;
using System.IO;
using System.Collections.Generic;
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
                @"TextureOverrides\stormcancer-Classfixes_Part_2\SideLoader\Items\-23285_Rune_ Wu\Textures\icon.png",
                @"stormcancer-Classfixes_Part_2\SideLoader\Items\-23285_Rune_ Wu\Textures\icon.png"
            ),
            // Runic Spirit Sword
            (
                @"stormcancer-Classfixes_Part_2\SideLoader\Items\-35248_Runic Spirit Sword\Textures\mat_itm_blackSteelWeapons\_AlphaTex.png",
                @"stormcancer-Classfixes_Part_2\SideLoader\Items\-35248_Runic Spirit Sword\Textures\mat_itm_longsword\_AlphaTex.png"
            )
        };

        private void Awake()
        {
            ReplaceFiles();
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
    }
}
