using System;
using System.Collections.Generic;
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

                        string[] blacklist = GetBlacklistEntries(pair, source);
                        string targetFolder = ResolveTargetFolderPath(target);
                        CopyFolderContents(sourceFolder, targetFolder, blacklist);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private void CopyFolderContents(string sourceFolder, string targetFolder, string[] blacklist)
        {
            string[] sourceFiles;
            try
            {
                sourceFiles = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to enumerate source folder '{sourceFolder}': {ex.Message}");
                return;
            }

            foreach (string sourceFile in sourceFiles)
            {
                try
                {
                    if (!File.Exists(sourceFile))
                    {
                        Logger.LogWarning($"Skipped missing source file during folder replace: {sourceFile}");
                        continue;
                    }

                    string relativePath = sourceFile.Substring(sourceFolder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (IsBlacklisted(sourceFile, relativePath, blacklist))
                    {
                        continue;
                    }

                    string destinationFile = Path.Combine(targetFolder, relativePath);
                    string destinationDir = Path.GetDirectoryName(destinationFile);
                    if (!string.IsNullOrWhiteSpace(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    File.Copy(sourceFile, destinationFile, true);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to copy '{sourceFile}' into '{targetFolder}': {ex.Message}");
                }
            }
        }

        private static string[] GetBlacklistEntries(JArray pair, string source)
        {
            List<string> blacklist = new List<string>();
            string normalizedSource = NormalizeRelativePath(source).TrimEnd(Path.DirectorySeparatorChar);

            foreach (JToken token in pair)
            {
                JObject options = token as JObject;
                if (options == null)
                {
                    continue;
                }

                JArray list = options["blacklist"] as JArray;
                if (list == null)
                {
                    continue;
                }

                foreach (JToken item in list)
                {
                    string raw = (string)item;
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    string normalized = NormalizeRelativePath(raw);
                    if (Path.IsPathRooted(normalized))
                    {
                        blacklist.Add("abs:" + normalized.TrimEnd(Path.DirectorySeparatorChar));
                        continue;
                    }

                    string relative = normalized;
                    if (!string.IsNullOrEmpty(normalizedSource) && normalized.StartsWith(normalizedSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        relative = normalized.Substring(normalizedSource.Length).TrimStart(Path.DirectorySeparatorChar);
                    }
                    else if (!string.IsNullOrEmpty(normalizedSource) && normalized.Equals(normalizedSource, StringComparison.OrdinalIgnoreCase))
                    {
                        relative = string.Empty;
                    }

                    blacklist.Add(relative.TrimEnd(Path.DirectorySeparatorChar));
                }
            }

            return blacklist.ToArray();
        }

        private static bool IsBlacklisted(string sourceFile, string relativePath, string[] blacklist)
        {
            if (blacklist == null || blacklist.Length == 0)
            {
                return false;
            }

            string normalizedSourceFile = NormalizeRelativePath(sourceFile).TrimEnd(Path.DirectorySeparatorChar);
            string normalizedRelativePath = NormalizeRelativePath(relativePath).TrimEnd(Path.DirectorySeparatorChar);

            foreach (string entry in blacklist)
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    return true;
                }

                if (entry.StartsWith("abs:", StringComparison.Ordinal))
                {
                    string absolute = entry.Substring(4).TrimEnd(Path.DirectorySeparatorChar);
                    if (normalizedSourceFile.Equals(absolute, StringComparison.OrdinalIgnoreCase) ||
                        normalizedSourceFile.StartsWith(absolute + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    continue;
                }

                string normalizedEntry = entry.TrimEnd(Path.DirectorySeparatorChar);
                if (normalizedRelativePath.Equals(normalizedEntry, StringComparison.OrdinalIgnoreCase) ||
                    normalizedRelativePath.StartsWith(normalizedEntry + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeRelativePath(string path)
        {
            return (path ?? string.Empty)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .Trim();
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
