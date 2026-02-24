using System;
using System.IO;
using BepInEx;

namespace FFT.Classfixes_Part_2
{
    [BepInPlugin("fierrof.fft.classfixes_part_2", "FFT.Classfixes_Part_2", "1.0.0")]
    public class ClassfixesPart2TexturePatchPlugin : BaseUnityPlugin
    {
        private const string RelativeTexturePath = @"SideLoader\Items\-23285_Rune_ Wu\Textures\icon.png";

        private void Awake()
        {
            try
            {
                string pluginDir = Path.GetDirectoryName(Info.Location) ?? string.Empty;
                string sourcePath = Path.Combine(pluginDir, "TextureOverrides", "stormcancer-Classfixes_Part_2", RelativeTexturePath);
                string targetPath = Path.Combine(Paths.PluginPath, "stormcancer-Classfixes_Part_2", RelativeTexturePath);

                if (!File.Exists(sourcePath)) return;

                string targetDir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(targetDir)) Directory.CreateDirectory(targetDir);
                File.Copy(sourcePath, targetPath, true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }
    }
}
