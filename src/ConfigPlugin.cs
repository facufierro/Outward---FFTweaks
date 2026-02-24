using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using Newtonsoft.Json.Linq;

namespace FFT.Config
{
    [BepInPlugin("fierrof.fft.config", "FFT.Config", "1.0.0")]
    public class ConfigPlugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> overwriteFirstInstallOnly;
        private ConfigEntry<bool> refreshNow;

        private void Awake()
        {
            ReplacementControl.Initialize(Info.Location, Logger);

            overwriteFirstInstallOnly = Config.Bind(
                "Replacements",
                "OverwriteFirstInstallOnly",
                false,
                "When true, replacements flagged as first-install-only will overwrite on startup and refresh.");

            refreshNow = Config.Bind(
                "Replacements",
                "RefreshNow",
                false,
                "Set true to run all replacement plugins immediately. It resets to false automatically.");

            ReplacementControl.SetOverwriteFirstInstallOnly(overwriteFirstInstallOnly.Value);

            overwriteFirstInstallOnly.SettingChanged += (_, __) =>
            {
                ReplacementControl.SetOverwriteFirstInstallOnly(overwriteFirstInstallOnly.Value);
            };

            refreshNow.SettingChanged += (_, __) =>
            {
                if (!refreshNow.Value)
                {
                    return;
                }

                ReplacementControl.RequestRefresh();
                refreshNow.Value = false;
            };
        }

        private void Start()
        {
            ReplacementControl.MarkInstallComplete();
        }
    }

    internal static class ReplacementControl
    {
        private static readonly object Sync = new object();
        private static bool initialized;
        private static bool firstInstall = true;
        private static bool overwriteFirstInstallOnly;
        private static string markerPath;
        private static BepInEx.Logging.ManualLogSource logger;

        internal static event Action RefreshRequested;

        internal static bool IsFirstInstall
        {
            get { lock (Sync) { return firstInstall; } }
        }

        internal static bool OverwriteFirstInstallOnly
        {
            get { lock (Sync) { return overwriteFirstInstallOnly; } }
        }

        internal static void Initialize(string pluginLocation, BepInEx.Logging.ManualLogSource sourceLogger)
        {
            lock (Sync)
            {
                if (initialized)
                {
                    return;
                }

                logger = sourceLogger;

                string pluginDir = Path.GetDirectoryName(pluginLocation) ?? string.Empty;
                markerPath = Path.Combine(pluginDir, ".fftweaks.install.complete");
                firstInstall = !File.Exists(markerPath);

                initialized = true;
            }
        }

        internal static void SetOverwriteFirstInstallOnly(bool value)
        {
            lock (Sync)
            {
                overwriteFirstInstallOnly = value;
            }
        }

        internal static void RequestRefresh()
        {
            RefreshRequested?.Invoke();
        }

        internal static void MarkInstallComplete()
        {
            lock (Sync)
            {
                if (!initialized || string.IsNullOrWhiteSpace(markerPath))
                {
                    return;
                }

                if (!File.Exists(markerPath))
                {
                    File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
                    logger?.LogInfo("First-install marker created.");
                }

                firstInstall = false;
            }
        }

        internal static bool ShouldApplyFlaggedReplacement(bool isFlagged)
        {
            if (!isFlagged)
            {
                return true;
            }

            return IsFirstInstall || OverwriteFirstInstallOnly;
        }

        internal static bool IsFlagEnabled(JArray tuple)
        {
            if (tuple == null || tuple.Count < 3)
            {
                return false;
            }
            return IsFlagEnabled(tuple[2]);
        }

        internal static bool IsFlagEnabled(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }

            if (token.Type == JTokenType.Boolean)
            {
                return token.Value<bool>();
            }

            string text = token.ToString().Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return string.Equals(text, "once", StringComparison.OrdinalIgnoreCase);
        }
    }
}