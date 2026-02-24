using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json.Linq;

namespace FFT.FileReplacer
{
    [BepInPlugin("fierrof.fft.filereplacer", "FFT.FileReplacer", "1.0.0")]
    public class FileReplacerPlugin : BaseUnityPlugin
    {
        private void Awake()
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
                    JArray files = root["files"] as JArray;
                    if (files != null)
                    {
                        foreach (JToken pairToken in files)
                        {
                            JArray pair = pairToken as JArray;
                            if (pair == null || pair.Count < 2) continue;

                            string source = ((string)pair[0] ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                            string target = ((string)pair[1] ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                            string sourcePath = ResolveSourcePath(source, dataRoot, pluginDir);
                            string targetPath = Path.IsPathRooted(target) ? target : Path.Combine(Paths.PluginPath, target);
                            if (!File.Exists(sourcePath)) continue;

                            string targetDir = Path.GetDirectoryName(targetPath);
                            if (!string.IsNullOrWhiteSpace(targetDir)) Directory.CreateDirectory(targetDir);

                            File.Copy(sourcePath, targetPath, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private static string ResolveSourcePath(string source, string dataRoot, string pluginDir)
        {
            if (Path.IsPathRooted(source))
            {
                return source;
            }

            string normalized = source.Replace('/', Path.DirectorySeparatorChar);
            if (normalized.StartsWith("_overrides" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                string fromDataRoot = Path.Combine(dataRoot, normalized);
                if (File.Exists(fromDataRoot))
                {
                    return fromDataRoot;
                }
            }

            const string prefix1 = "overrides\\";
            const string prefix2 = "overrides/";

            if (normalized.StartsWith(prefix1, StringComparison.OrdinalIgnoreCase) || normalized.StartsWith(prefix2, StringComparison.OrdinalIgnoreCase))
            {
                string relative = normalized.Substring("overrides".Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fromDataOverrides = Path.Combine(dataRoot, "_overrides", relative);
                if (File.Exists(fromDataOverrides))
                {
                    return fromDataOverrides;
                }
            }

            string fromPluginDir = Path.Combine(pluginDir, normalized);
            if (File.Exists(fromPluginDir))
            {
                return fromPluginDir;
            }

            return Path.Combine(Paths.PluginPath, normalized);
        }
    }
}
