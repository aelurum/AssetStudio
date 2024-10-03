using AssetStudio;
using AssetStudioCLI.Options;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;

namespace AssetStudioCLI
{
    internal static class ParallelExporter
    {
        private static ConcurrentDictionary<string, bool> savePathHash = new ConcurrentDictionary<string, bool>();

        public static bool ExportTexture2D(AssetItem item, string exportPath, out string debugLog)
        {
            debugLog = "";
            var m_Texture2D = (Texture2D)item.Asset;
            if (CLIOptions.convertTexture)
            {
                var type = CLIOptions.o_imageFormat.Value;
                if (!TryExportFile(exportPath, item, "." + type.ToString().ToLower(), out var exportFullPath))
                    return false;

                if (CLIOptions.o_logLevel.Value <= LoggerEvent.Debug)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Converting {item.TypeString} \"{m_Texture2D.m_Name}\" to {type}..");
                    sb.AppendLine($"Width: {m_Texture2D.m_Width}");
                    sb.AppendLine($"Height: {m_Texture2D.m_Height}");
                    sb.AppendLine($"Format: {m_Texture2D.m_TextureFormat}");
                    switch (m_Texture2D.m_TextureSettings.m_FilterMode)
                    {
                        case 0: sb.AppendLine("Filter Mode: Point "); break;
                        case 1: sb.AppendLine("Filter Mode: Bilinear "); break;
                        case 2: sb.AppendLine("Filter Mode: Trilinear "); break;
                    }
                    sb.AppendLine($"Anisotropic level: {m_Texture2D.m_TextureSettings.m_Aniso}");
                    sb.AppendLine($"Mip map bias: {m_Texture2D.m_TextureSettings.m_MipBias}");
                    switch (m_Texture2D.m_TextureSettings.m_WrapMode)
                    {
                        case 0: sb.AppendLine($"Wrap mode: Repeat"); break;
                        case 1: sb.AppendLine($"Wrap mode: Clamp"); break;
                    }
                    debugLog += sb.ToString();
                }

                var image = m_Texture2D.ConvertToImage(flip: true);
                if (image == null)
                {
                    Logger.Error($"{debugLog}Export error. Failed to convert texture \"{m_Texture2D.m_Name}\" into image");
                    return false;
                }
                using (image)
                {
                    using (var file = File.OpenWrite(exportFullPath))
                    {
                        image.WriteToStream(file, type);
                    }
                    debugLog += $"{item.TypeString} \"{item.Text}\" exported to \"{exportFullPath}\"";
                    return true;
                }
            }
            else
            {
                if (!TryExportFile(exportPath, item, ".tex", out var exportFullPath))
                    return false;
                File.WriteAllBytes(exportFullPath, m_Texture2D.image_data.GetData());
                debugLog += $"{item.TypeString} \"{item.Text}\" exported to \"{exportFullPath}\"";
                return true;
            }
        }

        public static bool ExportSprite(AssetItem item, string exportPath, out string debugLog)
        {
            debugLog = "";
            var type = CLIOptions.o_imageFormat.Value;
            var alphaMask = SpriteMaskMode.On;
            if (!TryExportFile(exportPath, item, "." + type.ToString().ToLower(), out var exportFullPath))
                return false;
            var image = ((Sprite)item.Asset).GetImage(alphaMask);
            if (image != null)
            {
                using (image)
                {
                    using (var file = File.OpenWrite(exportFullPath))
                    {
                        image.WriteToStream(file, type);
                    }
                    debugLog += $"{item.TypeString} \"{item.Text}\" exported to \"{exportFullPath}\"";
                    return true;
                }
            }
            return false;
        }

        public static bool ExportAudioClip(AssetItem item, string exportPath, out string debugLog)
        {
            debugLog = "";
            string exportFullPath;
            var m_AudioClip = (AudioClip)item.Asset;
            var m_AudioData = BigArrayPool<byte>.Shared.Rent(m_AudioClip.m_AudioData.Size);
            try
            {
                m_AudioClip.m_AudioData.GetData(m_AudioData);
                if (m_AudioData == null || m_AudioData.Length == 0)
                {
                    Logger.Error($"Export error. \"{item.Text}\": AudioData was not found");
                    return false;
                }
                var converter = new AudioClipConverter(m_AudioClip);
                if (CLIOptions.o_audioFormat.Value != AudioFormat.None && (converter.IsSupport || converter.IsLegacy))
                {
                    if (!TryExportFile(exportPath, item, ".wav", out exportFullPath))
                        return false;

                    if (CLIOptions.o_logLevel.Value <= LoggerEvent.Debug)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"Converting {item.TypeString} \"{m_AudioClip.m_Name}\" to wav..");
                        if (m_AudioClip.version >= (2, 6))
                        {
                            sb.AppendLine(m_AudioClip.version < 5
                                ? $"AudioClip type: {m_AudioClip.m_Type}"
                                : $"AudioClip compression format: {m_AudioClip.m_CompressionFormat}");
                            sb.AppendLine($"AudioClip channel count: {m_AudioClip.m_Channels}");
                            sb.AppendLine($"AudioClip sample rate: {m_AudioClip.m_Frequency}");
                            sb.AppendLine($"AudioClip bit depth: {m_AudioClip.m_BitsPerSample}");
                        }
                        else
                        {
                            sb.AppendLine($"Is raw AudioClip: {m_AudioClip.m_Format != 0x05}");
                            sb.AppendLine($"AudioClip channel count: {m_AudioClip.m_Channels}");
                            sb.AppendLine($"AudioClip sample rate: {m_AudioClip.m_Frequency}");
                        }
                        debugLog += sb.ToString();
                    }

                    var debugLogConverter = "";
                    var buffer = converter.IsLegacy
                        ? converter.RawAudioClipToWav(out debugLogConverter)
                        : converter.ConvertToWav(m_AudioData, out debugLogConverter);
                    debugLog += debugLogConverter;
                    if (buffer == null)
                    {
                        Logger.Error($"{debugLog}Export error. \"{item.Text}\": Failed to convert fmod audio to Wav");
                        return false;
                    }
                    File.WriteAllBytes(exportFullPath, buffer);
                }
                else
                {
                    if (!TryExportFile(exportPath, item, converter.GetExtensionName(), out exportFullPath))
                        return false;

                    if (CLIOptions.o_logLevel.Value <= LoggerEvent.Debug)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"Exporting non-fmod {item.TypeString} \"{m_AudioClip.m_Name}\"..");
                        if (m_AudioClip.version >= (2, 6))
                        {
                            sb.AppendLine(m_AudioClip.version < 5
                                ? $"AudioClip type: {m_AudioClip.m_Type}"
                                : $"AudioClip compression format: {m_AudioClip.m_CompressionFormat}");
                            sb.AppendLine($"AudioClip channel count: {m_AudioClip.m_Channels}");
                            sb.AppendLine($"AudioClip sample rate: {m_AudioClip.m_Frequency}");
                            sb.AppendLine($"AudioClip bit depth: {m_AudioClip.m_BitsPerSample}");
                        }
                        else
                        {
                            sb.AppendLine($"Is raw AudioClip: {m_AudioClip.m_Format != 0x05}");
                            sb.AppendLine($"AudioClip sample rate: {m_AudioClip.m_Frequency}");
                        }
                        debugLog += sb.ToString();
                    }
                    using (var file = File.OpenWrite(exportFullPath))
                    {
                        file.Write(m_AudioData, 0, m_AudioClip.m_AudioData.Size);
                    }
                }
                debugLog += $"{item.TypeString} \"{item.Text}\" exported to \"{exportFullPath}\"";
                return true;
            }
            finally
            {
                BigArrayPool<byte>.Shared.Return(m_AudioData, clearArray: true);
            }
        }

        private static bool TryExportFile(string dir, AssetItem item, string extension, out string fullPath)
        {
            var fileName = FixFileName(item.Text);
            var filenameFormat = CLIOptions.o_filenameFormat.Value;
            switch (filenameFormat)
            {
                case FilenameFormat.AssetName_PathID:
                    fileName = $"{fileName} @{item.m_PathID}";
                    break;
                case FilenameFormat.PathID:
                    fileName = item.m_PathID.ToString();
                    break;
            }
            fullPath = Path.Combine(dir, fileName + extension);
            if (savePathHash.TryAdd(fullPath.ToLower(), true) && !File.Exists(fullPath))
            {
                Directory.CreateDirectory(dir);
                return true;
            }
            if (filenameFormat == FilenameFormat.AssetName)
            {
                fullPath = Path.Combine(dir, fileName + item.UniqueID + extension);
                if (!File.Exists(fullPath))
                {
                    Directory.CreateDirectory(dir);
                    return true;
                }
            }
            Logger.Error($"Export error. File \"{fullPath.Color(ColorConsole.BrightRed)}\" already exist");
            return false;
        }

        public static bool ParallelExportConvertFile(AssetItem item, string exportPath, out string debugLog)
        {
            switch (item.Type)
            {
                case ClassIDType.Texture2D:
                case ClassIDType.Texture2DArrayImage:
                    return ExportTexture2D(item, exportPath, out debugLog);
                case ClassIDType.Sprite:
                    return ExportSprite(item, exportPath, out debugLog);
                case ClassIDType.AudioClip:
                    return ExportAudioClip(item, exportPath, out debugLog);
                default:
                    throw new NotImplementedException();
            }
        }

        private static string FixFileName(string str)
        {
            return str.Length >= 260 
                ? Path.GetRandomFileName() 
                : Path.GetInvalidFileNameChars().Aggregate(str, (current, c) => current.Replace(c, '_'));
        }

        public static void ClearHash()
        {
            savePathHash.Clear();
        }
    }
}
