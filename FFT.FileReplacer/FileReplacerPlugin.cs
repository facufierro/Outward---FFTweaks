using System;
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
                string dataPath = Path.Combine(pluginDir, "data.json");
                if (!File.Exists(dataPath))
                {
                    Logger.LogWarning($"data.json not found: {dataPath}");
                    return;
                }

                JObject root = JObject.Parse(File.ReadAllText(dataPath));
                foreach (JProperty config in root.Properties())
                {
                    JArray files = config.Value["files"] as JArray;
                    if (files == null) continue;

                    foreach (JToken pairToken in files)
                    {
                        JArray pair = pairToken as JArray;
                        if (pair == null || pair.Count < 2) continue;

                        string source = ((string)pair[0] ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                        string target = ((string)pair[1] ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
                        string sourcePath = Path.IsPathRooted(source) ? source : Path.Combine(pluginDir, source);
                        string targetPath = Path.IsPathRooted(target) ? target : Path.Combine(Paths.PluginPath, target);
                        if (!File.Exists(sourcePath)) continue;

                        string targetDir = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrWhiteSpace(targetDir)) Directory.CreateDirectory(targetDir);

                        File.Copy(sourcePath, targetPath, true);
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
