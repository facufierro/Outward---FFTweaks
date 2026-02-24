using System;
using System.IO;
using System.Collections.Generic;
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
                string dataPath = Path.Combine(pluginDir, "data.json");
                if (!File.Exists(dataPath))
                {
                    Logger.LogWarning($"data.json not found: {dataPath}");
                    return;
                }

                JObject root = JObject.Parse(File.ReadAllText(dataPath));
                Stack<JToken> pending = new Stack<JToken>();
                pending.Push(root);

                while (pending.Count > 0)
                {
                    JToken token = pending.Pop();
                    if (token is JProperty property)
                    {
                        pending.Push(property.Value);
                        continue;
                    }

                    if (token is JArray array)
                    {
                        foreach (JToken child in array)
                        {
                            pending.Push(child);
                        }
                        continue;
                    }

                    JObject node = token as JObject;
                    if (node == null) continue;

                    JArray files = node["files"] as JArray;
                    if (files != null)
                    {
                        foreach (JToken pairToken in files)
                        {
                            JArray pair = pairToken as JArray;
                            if (pair == null || pair.Count < 2) continue;

                            string source = ((string)pair[0] ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                            string target = ((string)pair[1] ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                            string sourcePath = Path.IsPathRooted(source) ? source : Path.Combine(pluginDir, source);
                            if (!Path.IsPathRooted(source) && !File.Exists(sourcePath))
                            {
                                string pluginsRootSourcePath = Path.Combine(Paths.PluginPath, source);
                                if (File.Exists(pluginsRootSourcePath))
                                {
                                    sourcePath = pluginsRootSourcePath;
                                }
                            }

                            string targetPath = Path.IsPathRooted(target) ? target : Path.Combine(Paths.PluginPath, target);
                            if (!File.Exists(sourcePath)) continue;

                            string targetDir = Path.GetDirectoryName(targetPath);
                            if (!string.IsNullOrWhiteSpace(targetDir)) Directory.CreateDirectory(targetDir);

                            File.Copy(sourcePath, targetPath, true);
                        }
                    }

                    foreach (JProperty childProperty in node.Properties())
                    {
                        pending.Push(childProperty.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }
    }
}
