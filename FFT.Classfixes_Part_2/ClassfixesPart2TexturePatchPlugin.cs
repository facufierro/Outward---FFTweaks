using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace FFT.Classfixes_Part_2
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class ClassfixesPart2TexturePatchPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "fierrof.fft.classfixes_part_2";
        private const string PluginName = "FFT.Classfixes_Part_2";
        private const string PluginVersion = "1.0.0";
        private const float RetryIntervalSeconds = 10f;
        private const int MaxApplyAttempts = 12;
        private const string EmbeddedResourcePrefix = "FFTOverride/";
        private static readonly string[] NonSpiritSwordItemIds =
        {
            "-35173_Runic Fire Sword",
            "-35174_Runic Frost Sword",
            "-35175_Runic Decay Sword",
            "-35176_Runic Electric Sword",
            "-35177_Runic Ethereal Sword",
            "-35178_Runic Blood Sword",
            "-35179_Runic Wind Sword",
            "-35180_Runic Earth Sword"
        };
        private static readonly string[] CriticalMaceAndGreathammerItemIds =
        {
            "-35189_Runic Fire Mace",
            "-35190_Runic Frost Mace",
            "-35191_Runic Decay Mace",
            "-35192_Runic Electric Mace",
            "-35193_Runic Ethereal Mace",
            "-35194_Runic Blood Mace",
            "-35195_Runic Wind Mace",
            "-35196_Runic Earth Mace",
            "-35197_Runic Fire Greathammer",
            "-35198_Runic Frost Greathammer",
            "-35199_Runic Decay Greathammer",
            "-35200_Runic Electric Greathammer",
            "-35201_Runic Ethereal Greathammer",
            "-35202_Runic Blood Greathammer",
            "-35203_Runic Wind Greathammer",
            "-35204_Runic Earth Greathammer",
            "-35157_Runic Fire Axe",
            "-35158_Runic Frost Axe",
            "-35159_Runic Decay Axe",
            "-35160_Runic Electric Axe",
            "-35161_Runic Ethereal Axe",
            "-35162_Runic Blood Axe",
            "-35163_Runic Wind Axe",
            "-35164_Runic Earth Axe",
            "-35165_Runic Fire Greataxe",
            "-35166_Runic Frost Greataxe",
            "-35167_Runic Decay Greataxe",
            "-35168_Runic Electric Greataxe",
            "-35169_Runic Ethereal Greataxe",
            "-35170_Runic Blood Greataxe",
            "-35171_Runic Wind Greataxe",
            "-35172_Runic Earth Greataxe",
            "-35205_Runic Fire Spear",
            "-35206_Runic Frost Spear",
            "-35207_Runic Decay Spear",
            "-35208_Runic Electric Spear",
            "-35209_Runic Ethereal Spear",
            "-35210_Runic Blood Spear",
            "-35211_Runic Wind Spear",
            "-35212_Runic Earth Spear",
            "-35246_Runic Spirit Axe",
            "-35247_Runic Spirit Greataxe",
            "-35250_Runic Spirit Mace",
            "-35251_Runic Spirit Greathammer",
            "-35252_Runic Spirit Spear"
        };

        private int _applyAttempts;
        private float _nextAttemptTime;

        private void Awake()
        {
            TryApplyOverrides("Awake");
        }

        private void Update()
        {
            if (_applyAttempts >= MaxApplyAttempts)
            {
                return;
            }

            if (Time.unscaledTime < _nextAttemptTime)
            {
                return;
            }

            _nextAttemptTime = Time.unscaledTime + RetryIntervalSeconds;
            TryApplyOverrides("Retry");
        }

        private void TryApplyOverrides(string source)
        {
            _applyAttempts++;

            try
            {
                string destinationRoot = Path.Combine(Paths.PluginPath, "stormcancer-Classfixes_Part_2");
                string sourceRoot = ResolveSourceRoot();

                if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
                {
                    Logger.LogWarning($"[{source}] Texture override source not found (attempt {_applyAttempts}/{MaxApplyAttempts}).");
                    return;
                }

                if (!Directory.Exists(destinationRoot))
                {
                    Logger.LogWarning($"[{source}] Target mod folder not found: {destinationRoot}");
                    return;
                }

                Logger.LogInfo($"[{source}] Override source root: {sourceRoot}");
                CleanupLegacySwordTextureOverrides(destinationRoot, source);
                ForceRefreshCriticalSpiritOverrides(sourceRoot, destinationRoot, source);

                int copied = 0;
                var skipSourceDirs = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string sourceFile in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
                {
                    string sourceDir = Path.GetDirectoryName(sourceFile) ?? string.Empty;
                    if (skipSourceDirs.Contains(sourceDir))
                        continue;

                    // If this directory only contains properties (no png textures) and the destination already
                    // has pngs for the same material, skip copying to avoid wiping the installed textures.
                    string fileName = Path.GetFileName(sourceFile);
                    if (string.Equals(fileName, "properties.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        bool sourceHasPng = Directory.GetFiles(sourceDir, "*.png").Length > 0;
                        string relativeDir = sourceDir.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        string correspondingDestDir = Path.Combine(destinationRoot, relativeDir);
                        bool destHasPng = Directory.Exists(correspondingDestDir) && Directory.GetFiles(correspondingDestDir, "*.png").Length > 0;

                        if (!sourceHasPng && destHasPng)
                        {
                            // Skip this entire source directory
                            skipSourceDirs.Add(sourceDir);
                            continue;
                        }
                    }

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

                Logger.LogInfo($"[{source}] Applied Classfixes Part 2 texture overrides: {copied} file(s)");
                _applyAttempts = MaxApplyAttempts;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[{source}] Failed to apply Classfixes Part 2 texture overrides: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private string ResolveSourceRoot()
        {
            string pluginDir = Path.GetDirectoryName(Info.Location) ?? string.Empty;

            string[] candidates =
            {
                Path.Combine(pluginDir, "TextureOverrides", "stormcancer-Classfixes_Part_2"),
                Path.Combine(pluginDir, "stormcancer-Classfixes_Part_2"),
                Path.Combine(Paths.PluginPath, "fierrof-FFTweaks", "TextureOverrides", "stormcancer-Classfixes_Part_2")
            };

            foreach (string candidate in candidates)
            {
                if (!Directory.Exists(candidate))
                {
                    continue;
                }

                string sideloaderDir = Path.Combine(candidate, "SideLoader");
                if (Directory.Exists(sideloaderDir))
                {
                    return candidate;
                }
            }

            string extractedRoot = ExtractEmbeddedOverrides(pluginDir);
            if (!string.IsNullOrWhiteSpace(extractedRoot) && Directory.Exists(Path.Combine(extractedRoot, "SideLoader")))
            {
                return extractedRoot;
            }

            return string.Empty;
        }

        private string ExtractEmbeddedOverrides(string pluginDir)
        {
            string outputRoot = Path.Combine(pluginDir, "_eo", "stormcancer-Classfixes_Part_2");
            const string leadingRoot = "stormcancer-Classfixes_Part_2";

            try
            {
                Assembly assembly = GetType().Assembly;
                string[] resourceNames = assembly
                    .GetManifestResourceNames()
                    .Where(name => name.StartsWith(EmbeddedResourcePrefix, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (resourceNames.Length == 0)
                {
                    return string.Empty;
                }

                if (Directory.Exists(outputRoot))
                {
                    Directory.Delete(outputRoot, true);
                }

                foreach (string resourceName in resourceNames)
                {
                    string relative = resourceName.Substring(EmbeddedResourcePrefix.Length)
                        .Replace('/', Path.DirectorySeparatorChar)
                        .Replace('\\', Path.DirectorySeparatorChar);

                    if (relative.StartsWith(leadingRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        relative = relative.Substring(leadingRoot.Length + 1);
                    }

                    string destination = Path.Combine(outputRoot, relative);
                    string destinationDir = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(destinationDir) && !Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    using (Stream sourceStream = assembly.GetManifestResourceStream(resourceName))
                    {
                        if (sourceStream == null)
                        {
                            continue;
                        }

                        using (FileStream destinationStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.Read))
                        {
                            sourceStream.CopyTo(destinationStream);
                        }
                    }
                }

                return outputRoot;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to extract embedded overrides: {ex.GetType().Name}: {ex.Message}");
                return string.Empty;
            }
        }

        private void CleanupLegacySwordTextureOverrides(string destinationRoot, string source)
        {
            int removed = 0;
            foreach (string itemId in NonSpiritSwordItemIds)
            {
                string legacyDir = Path.Combine(destinationRoot, "SideLoader", "Items", itemId, "Textures", "mat_itm_longsword");
                if (!Directory.Exists(legacyDir))
                {
                    continue;
                }

                Directory.Delete(legacyDir, true);
                removed++;
            }

            if (removed > 0)
            {
                Logger.LogInfo($"[{source}] Removed {removed} stale non-spirit sword texture override folder(s).");
            }
        }

        private void ForceRefreshCriticalSpiritOverrides(string sourceRoot, string destinationRoot, string source)
        {
            int refreshed = 0;
            foreach (string itemId in CriticalMaceAndGreathammerItemIds)
            {
                string sourceItemDir = Path.Combine(sourceRoot, "SideLoader", "Items", itemId);
                string destinationItemDir = Path.Combine(destinationRoot, "SideLoader", "Items", itemId);

                if (!Directory.Exists(sourceItemDir))
                {
                    Logger.LogWarning($"[{source}] Critical source folder missing: {sourceItemDir}");
                    continue;
                }

                if (Directory.Exists(destinationItemDir))
                {
                    Directory.Delete(destinationItemDir, true);
                }

                CopyDirectory(sourceItemDir, destinationItemDir);

                int sourceFileCount = Directory.GetFiles(sourceItemDir, "*", SearchOption.AllDirectories).Length;
                int destinationFileCount = Directory.GetFiles(destinationItemDir, "*", SearchOption.AllDirectories).Length;
                Logger.LogInfo($"[{source}] Force-refreshed {itemId}: {sourceFileCount} -> {destinationFileCount} file(s)");
                refreshed++;
            }

            if (refreshed > 0)
            {
                Logger.LogInfo($"[{source}] Force-refreshed {refreshed} critical mace/greathammer item folder(s).");
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destinationFile = Path.Combine(destinationDir, fileName);
                File.Copy(file, destinationFile, true);
            }

            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                string directoryName = Path.GetFileName(directory);
                if (string.IsNullOrWhiteSpace(directoryName))
                {
                    continue;
                }

                string destinationSubDir = Path.Combine(destinationDir, directoryName);
                CopyDirectory(directory, destinationSubDir);
            }
        }

    }
}
