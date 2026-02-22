using System;
using System.IO;
using BepInEx;

namespace FFT.Classfixes_Part_2
{
    [BepInPlugin("fierrof.fft.classfixes_part_2", "FFT.Classfixes_Part_2", "1.0.0")]
    public class ClassfixesPart2TexturePatchPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            try
            {
                string pluginDir = Path.GetDirectoryName(Info.Location);
                string destinationRoot = Path.Combine(Paths.PluginPath, "stormcancer-Classfixes_Part_2");

                string sourceRoot = Path.Combine(pluginDir ?? string.Empty, "TextureOverrides", "stormcancer-Classfixes_Part_2");
                if (!Directory.Exists(sourceRoot))
                {
                    string legacyRoot = pluginDir ?? string.Empty;
                    string legacyIcon = Path.Combine(legacyRoot, "icon.png");
                    string legacySkillIcon = Path.Combine(legacyRoot, "skillicon.png");
                    if (File.Exists(legacyIcon) && File.Exists(legacySkillIcon))
                    {
                        sourceRoot = Path.Combine(legacyRoot, "_legacy_rune_wu_source");
                        string sourceTextures = Path.Combine(sourceRoot, "SideLoader", "Items", "-23285_Rune_ Wu", "Textures");
                        Directory.CreateDirectory(sourceTextures);
                        File.Copy(legacyIcon, Path.Combine(sourceTextures, "icon.png"), true);
                        File.Copy(legacySkillIcon, Path.Combine(sourceTextures, "skillicon.png"), true);
                    }
                }

                if (!Directory.Exists(sourceRoot))
                {
                    Logger.LogWarning($"Texture override source not found: {sourceRoot}");
                    return;
                }

                if (!Directory.Exists(destinationRoot))
                {
                    Logger.LogWarning($"Target mod folder not found: {destinationRoot}");
                    return;
                }

                int copied = 0;
                foreach (string sourceFile in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
                {
                    string relative = sourceFile.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string destinationFile = Path.Combine(destinationRoot, relative);
                    string destinationDir = Path.GetDirectoryName(destinationFile);
                    if (!string.IsNullOrWhiteSpace(destinationDir) && !Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    File.Copy(sourceFile, destinationFile, true);
                    copied++;
                }

                Logger.LogInfo($"Applied Classfixes Part 2 texture overrides: {copied} file(s)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to apply Classfixes Part 2 texture overrides: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
