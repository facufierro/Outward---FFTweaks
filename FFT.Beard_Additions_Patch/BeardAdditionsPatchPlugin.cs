using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace FFT.Beard_Additions_Patch
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class BeardAdditionsPatchPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "fierrof.fft.beard_additions_patch";
        private const string PluginName = "FFT.Beard_Additions_Patch";
        private const string PluginVersion = "1.0.0";

        private const string TargetModFolder = "stormcancer-Beard_Additions";
        private const float RetryIntervalSeconds = 10f;
        private const int MaxAttempts = 12;

        private int _attempts;
        private float _nextAttemptTime;

        private void Awake()
        {
            TryApply("Awake");
        }

        private void Update()
        {
            if (_attempts >= MaxAttempts || Time.unscaledTime < _nextAttemptTime)
            {
                return;
            }

            _nextAttemptTime = Time.unscaledTime + RetryIntervalSeconds;
            TryApply("Retry");
        }

        private void TryApply(string source)
        {
            _attempts++;

            try
            {
                string sourceRoot = ResolveSourceRoot();
                string destinationRoot = Path.Combine(Paths.PluginPath, TargetModFolder);

                if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
                {
                    Logger.LogWarning($"[{source}] Beard Additions source not found (attempt {_attempts}/{MaxAttempts}).");
                    return;
                }

                if (!Directory.Exists(destinationRoot))
                {
                    Logger.LogWarning($"[{source}] Beard Additions target mod folder not found: {destinationRoot}");
                    return;
                }

                int copied = CopyAllFiles(sourceRoot, destinationRoot);
                Logger.LogInfo($"[{source}] Applied Beard Additions overrides: {copied} file(s)");
                _attempts = MaxAttempts;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[{source}] Failed to apply Beard Additions overrides: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private string ResolveSourceRoot()
        {
            string pluginDir = Path.GetDirectoryName(Info.Location) ?? string.Empty;

            string[] candidates =
            {
                Path.Combine(pluginDir, "TextureOverrides", TargetModFolder),
                Path.Combine(pluginDir, TargetModFolder),
                Path.Combine(Paths.PluginPath, "fierrof-FFTweaks", "TextureOverrides", TargetModFolder)
            };

            foreach (string candidate in candidates)
            {
                if (!Directory.Exists(candidate))
                {
                    continue;
                }

                if (Directory.Exists(Path.Combine(candidate, "Sideloader")) || Directory.Exists(Path.Combine(candidate, "SideLoader")))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static int CopyAllFiles(string sourceRoot, string destinationRoot)
        {
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

            return copied;
        }
    }
}
