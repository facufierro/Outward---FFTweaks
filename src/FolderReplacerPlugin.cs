using System;
using System.IO;
using BepInEx;
using FFT.Config;
using Newtonsoft.Json.Linq;

namespace FFT.FolderReplacer
{
    [BepInPlugin("fierrof.fft.folderreplacer", "FFT.FolderReplacer", "1.0.0")]
    public class FolderReplacerPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            ReplacementControl.Initialize(Info.Location, Logger);
            ReplacementControl.RefreshRequested += Run;
            Run();
        }

        private void Run()
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
                    string jsonDir = Path.GetDirectoryName(jsonPath) ?? dataRoot;
                    JObject root = JObject.Parse(File.ReadAllText(jsonPath));
                    JArray folders = root["folders"] as JArray;
                    if (folders == null) continue;

                    foreach (JToken pairToken in folders)
                    {
                        JArray pair = pairToken as JArray;
                        if (pair == null || pair.Count < 2) continue;
                        bool firstInstallOnly = ReplacementControl.IsFlagEnabled(pair);
                        if (!ReplacementControl.ShouldApplyFlaggedReplacement(firstInstallOnly)) continue;

                        string source = ((string)pair[0] ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                        string target = ((string)pair[1] ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) continue;

                        string sourceFolder = ResolveSourceFolderPath(source, dataRoot, pluginDir, jsonDir);
                        if (!Directory.Exists(sourceFolder)) continue;

                        string targetFolder = ResolveTargetFolderPath(target);
                        CopyFolderContents(sourceFolder, targetFolder);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private static void CopyFolderContents(string sourceFolder, string targetFolder)
        {
            foreach (string sourceFile in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                string relativePath = sourceFile.Substring(sourceFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destinationFile = Path.Combine(targetFolder, relativePath);
                string destinationDir = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrWhiteSpace(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                File.Copy(sourceFile, destinationFile, true);
            }
        }

        private static string ResolveSourceFolderPath(string source, string dataRoot, string pluginDir, string jsonDir)
        {
            if (Path.IsPathRooted(source))
            {
                return source;
            }

            string normalized = source.Replace('/', Path.DirectorySeparatorChar);

            const string dataPrefix = "data";
            if (normalized.StartsWith(dataPrefix + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                string relativeToData = normalized.Substring(dataPrefix.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                string fromDataRoot = Path.Combine(dataRoot, relativeToData);
                if (Directory.Exists(fromDataRoot))
                {
                    return fromDataRoot;
                }

                string currentJsonFolderName = new DirectoryInfo(jsonDir).Name;
                if (relativeToData.StartsWith(currentJsonFolderName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    string relativeToJsonFolder = relativeToData.Substring(currentJsonFolderName.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string fromJsonFolder = Path.Combine(jsonDir, relativeToJsonFolder);
                    if (Directory.Exists(fromJsonFolder))
                    {
                        return fromJsonFolder;
                    }
                }
            }

            string fromPluginDir = Path.Combine(pluginDir, normalized);
            if (Directory.Exists(fromPluginDir))
            {
                return fromPluginDir;
            }

            return Path.Combine(Paths.PluginPath, normalized);
        }

        private static string ResolveTargetFolderPath(string target)
        {
            if (Path.IsPathRooted(target))
            {
                return target;
            }

            string normalized = target.Replace('/', Path.DirectorySeparatorChar);
            string bepInExRoot = Directory.GetParent(Paths.PluginPath)?.FullName ?? Paths.PluginPath;
            return Path.Combine(bepInExRoot, normalized);
        }
    }
}
