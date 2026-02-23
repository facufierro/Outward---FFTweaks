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
    }
}
