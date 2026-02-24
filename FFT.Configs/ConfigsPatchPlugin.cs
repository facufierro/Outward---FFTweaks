using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace FFT.Configs
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class ConfigsPatchPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "fierrof.fft.configs";
        private const string PluginName = "FFT.Configs";
        private const string PluginVersion = "1.0.0";
        private const string MarkerFileName = "fierrof.fft.configs.initialized";

        private const float RetryIntervalSeconds = 10f;
        private const int MaxAttempts = 12;

        private int _attempts;
        private float _nextAttemptTime;
        private ConfigEntry<bool> _overrideConfigs;
        private ConfigEntry<string> _overrideNowButton;
        private ConfigEntry<string> _lastOverrideStatus;

        private void Awake()
        {
            InitializeConfigEntries();
            TryApply("Awake", ignoreMarker: false);
        }

        private void Update()
        {
            if (_attempts >= MaxAttempts || Time.unscaledTime < _nextAttemptTime)
            {
                return;
            }

            _nextAttemptTime = Time.unscaledTime + RetryIntervalSeconds;
            TryApply("Retry", ignoreMarker: false);
        }

        private void TryApply(string source, bool ignoreMarker)
        {
            if (!ignoreMarker)
            {
                _attempts++;
            }

            try
            {
                string sourceRoot = ResolveSourceRoot();
                string destinationRoot = Paths.ConfigPath;

                if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
                {
                    Logger.LogWarning($"[{source}] Config override source not found (attempt {_attempts}/{MaxAttempts}).");
                    return;
                }

                if (string.IsNullOrWhiteSpace(destinationRoot) || !Directory.Exists(destinationRoot))
                {
                    Logger.LogWarning($"[{source}] BepInEx config folder not found: {destinationRoot}");
                    return;
                }

                string markerPath = GetMarkerPath(destinationRoot);
                if (!ignoreMarker && File.Exists(markerPath))
                {
                    Logger.LogInfo($"[{source}] Config overrides already initialized. Skipping overwrite.");
                    _attempts = MaxAttempts;
                    return;
                }

                int copied = CopyAllFiles(sourceRoot, destinationRoot);
                DateTime nowUtc = DateTime.UtcNow;
                File.WriteAllText(markerPath, $"{PluginVersion}|{nowUtc:O}");
                UpdateLastOverrideStatus(nowUtc);
                Logger.LogInfo($"[{source}] Applied config overrides: {copied} file(s)");

                if (!ignoreMarker)
                {
                    _attempts = MaxAttempts;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[{source}] Failed to apply config overrides: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void UpdateLastOverrideStatus(DateTime utcTimestamp)
        {
            if (_lastOverrideStatus == null)
            {
                return;
            }

            _lastOverrideStatus.Value = utcTimestamp.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
            Config.Save();
        }

        private void TryInitializeStatusFromMarker()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Paths.ConfigPath) || !Directory.Exists(Paths.ConfigPath))
                {
                    return;
                }

                string markerPath = GetMarkerPath(Paths.ConfigPath);
                if (!File.Exists(markerPath))
                {
                    return;
                }

                string markerText = File.ReadAllText(markerPath);
                string[] parts = markerText.Split('|');
                if (parts.Length >= 2 && DateTime.TryParse(parts[1], out DateTime parsedUtc))
                {
                    UpdateLastOverrideStatus(parsedUtc.ToUniversalTime());
                    return;
                }

                UpdateLastOverrideStatus(File.GetLastWriteTimeUtc(markerPath));
            }
            catch
            {
            }
        }

        private void InitializeConfigEntries()
        {
            _overrideConfigs = Config.Bind(
                "Manual Override",
                "Override Configs",
                false,
                "When enabled, applies bundled config overrides immediately once and then turns itself off.");

            _overrideConfigs.SettingChanged += OnOverrideConfigsChanged;

            _overrideNowButton = Config.Bind(
                "Manual Override",
                "Override Configs Now",
                "Click",
                new ConfigDescription(
                    "Button that immediately applies bundled config overrides.",
                    null,
                    new object[]
                    {
                        new ConfigurationManagerAttributes
                        {
                            CustomDrawer = DrawOverrideNowButton
                        }
                    }));

            _lastOverrideStatus = Config.Bind(
                "Manual Override",
                "Last Override (UTC)",
                "Never",
                new ConfigDescription(
                    "Timestamp of the last successful config override.",
                    null,
                    new object[]
                    {
                        new ConfigurationManagerAttributes
                        {
                            ReadOnly = true,
                            Order = 10
                        }
                    }));

            if (string.Equals(_lastOverrideStatus.Value, "Never", StringComparison.OrdinalIgnoreCase))
            {
                TryInitializeStatusFromMarker();
            }
        }

        private void OnOverrideConfigsChanged(object sender, EventArgs args)
        {
            if (_overrideConfigs == null || !_overrideConfigs.Value)
            {
                return;
            }

            TryApply("ConfigToggle", ignoreMarker: true);
            _overrideConfigs.Value = false;
            Config.Save();
        }

        private void DrawOverrideNowButton(ConfigEntryBase entry)
        {
            if (GUILayout.Button("Override Configs Now", GUILayout.ExpandWidth(true)))
            {
                TryApply("ConfigButton", ignoreMarker: true);
            }
        }

        private static string GetMarkerPath(string destinationRoot)
        {
            return Path.Combine(destinationRoot, MarkerFileName);
        }

        private string ResolveSourceRoot()
        {
            string pluginDir = Path.GetDirectoryName(Info.Location) ?? string.Empty;

            string[] candidates =
            {
                Path.Combine(pluginDir, "overrides"),
                Path.Combine(pluginDir, "FFT.Configs", "overrides"),
                Path.Combine(Paths.PluginPath, "fierrof-FFTweaks", "overrides")
            };

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(candidate))
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

        private sealed class ConfigurationManagerAttributes
        {
            public Action<ConfigEntryBase> CustomDrawer;
            public bool ReadOnly;
            public int? Order;
        }
    }
}