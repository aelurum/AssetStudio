using FMOD;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AssetStudio
{
    public class AudioClipConverter
    {
        public bool IsSupport => m_AudioClip.IsConvertSupport();
        public bool IsLegacy => m_AudioClip.IsLegacyConvertSupport();

        private AudioClip m_AudioClip;
        private static FMOD.System system;

        static AudioClipConverter()
        {
            var result = Factory.System_Create(out system);
            if (result != RESULT.OK)
            {
                Logger.Error($"FMOD error! {result} - {Error.String(result)}");
            }
            result = system.init(1, INITFLAGS.NORMAL, IntPtr.Zero);
            if (result != RESULT.OK)
            {
                Logger.Error($"FMOD error! {result} - {Error.String(result)}");
            }
        }

        public AudioClipConverter(AudioClip audioClip)
        {
            m_AudioClip = audioClip;
        }

        public byte[] ConvertToWav(byte[] m_AudioData, out string debugLog)
        {
            debugLog = "";
            var exinfo = new CREATESOUNDEXINFO();
            exinfo.cbsize = Marshal.SizeOf(exinfo);
            exinfo.length = (uint)m_AudioClip.m_Size;
            var result = system.createSound(m_AudioData, MODE.OPENMEMORY, ref exinfo, out var sound);
            if (result != RESULT.OK)
                return null;
            result = sound.getNumSubSounds(out var numsubsounds);
            if (result != RESULT.OK)
                return null;
            byte[] buff;
            if (numsubsounds > 0)
            {
                result = sound.getSubSound(0, out var subsound);
                if (result != RESULT.OK)
                    return null;
                buff = SoundToWav(subsound, out debugLog);
                subsound.release();
                subsound.clearHandle();
            }
            else
            {
                buff = SoundToWav(sound, out debugLog);
            }
            sound.release();
            sound.clearHandle();
            return buff;
        }

        public byte[] SoundToWav(Sound sound, out string debugLog)
        {
            debugLog = "[Fmod] Detecting sound format..\n";
            var result = sound.getFormat(out SOUND_TYPE soundType, out SOUND_FORMAT soundFormat, out int channels, out int bits);
            if (result != RESULT.OK)
                return null;
            debugLog += $"Detected sound type: {soundType}\n" +
                        $"Detected sound format: {soundFormat}\n" +
                        $"Detected channels: {channels}\n" +
                        $"Detected bit depth: {bits}\n";
            result = sound.getDefaults(out var frequency, out _);
            if (result != RESULT.OK)
                return null;
            var sampleRate = (int)frequency;
            result = sound.getLength(out var length, TIMEUNIT.PCMBYTES);
            if (result != RESULT.OK)
                return null;
            result = sound.@lock(0, length, out var ptr1, out var ptr2, out var len1, out var len2);
            if (result != RESULT.OK)
                return null;
            var buffer = new byte[len1 + 44];
            //添加wav头
            WriteWavHeader(buffer, len1, sampleRate, channels, bits);
            Marshal.Copy(ptr1, buffer, 44, (int)len1);
            result = sound.unlock(ptr1, ptr2, len1, len2);
            if (result != RESULT.OK)
                return null;
            return buffer;
        }

        public byte[] RawAudioClipToWav(out string debugLog)
        {
            var audioSize = m_AudioClip.m_Size;
            var channels = m_AudioClip.m_Channels;
            var sampleRate = m_AudioClip.m_Frequency;
            var bits = 16;

            debugLog = "[Legacy wav converter] Generating wav header..\n";
            var buffer = new byte[audioSize + 44];
            m_AudioClip.m_AudioData.GetData(buffer, 44);
            WriteWavHeader(buffer, audioSize, sampleRate, channels, bits);
            return buffer;
        }

        private static void WriteWavHeader(byte[] buffer, long size, int sampleRate, int channels, int bits)
        {
            Encoding.ASCII.GetBytes("RIFF").CopyTo(buffer, 0);
            BitConverter.GetBytes(size + 36).CopyTo(buffer, 4);
            Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(buffer, 8);
            BitConverter.GetBytes(16).CopyTo(buffer, 16);
            BitConverter.GetBytes((short)1).CopyTo(buffer, 20);
            BitConverter.GetBytes((short)channels).CopyTo(buffer, 22);
            BitConverter.GetBytes(sampleRate).CopyTo(buffer, 24);
            BitConverter.GetBytes(sampleRate * channels * bits / 8).CopyTo(buffer, 28);
            BitConverter.GetBytes((short)(channels * bits / 8)).CopyTo(buffer, 32);
            BitConverter.GetBytes((short)bits).CopyTo(buffer, 34);
            Encoding.ASCII.GetBytes("data").CopyTo(buffer, 36);
            BitConverter.GetBytes(size).CopyTo(buffer, 40);
        }

        public string GetExtensionName()
        {
            if (m_AudioClip.version < 5)
            {
                switch (m_AudioClip.m_Type)
                {
                    case FMODSoundType.AAC:
                        return ".m4a";
                    case FMODSoundType.AIFF:
                        return ".aif";
                    case FMODSoundType.IT:
                        return ".it";
                    case FMODSoundType.MOD:
                        return ".mod";
                    case FMODSoundType.MPEG:
                        return ".mp3";
                    case FMODSoundType.OGGVORBIS:
                        return ".ogg";
                    case FMODSoundType.S3M:
                        return ".s3m";
                    case FMODSoundType.WAV:
                        return ".wav";
                    case FMODSoundType.XM:
                        return ".xm";
                    case FMODSoundType.XMA:
                        return ".wav";
                    case FMODSoundType.VAG:
                        return ".vag";
                    case FMODSoundType.AUDIOQUEUE:
                        return ".fsb";
                }

            }
            else
            {
                switch (m_AudioClip.m_CompressionFormat)
                {
                    case AudioCompressionFormat.PCM:
                        return ".fsb";
                    case AudioCompressionFormat.Vorbis:
                        return ".fsb";
                    case AudioCompressionFormat.ADPCM:
                        return ".fsb";
                    case AudioCompressionFormat.MP3:
                        return ".fsb";
                    case AudioCompressionFormat.PSMVAG:
                        return ".fsb";
                    case AudioCompressionFormat.HEVAG:
                        return ".fsb";
                    case AudioCompressionFormat.XMA:
                        return ".fsb";
                    case AudioCompressionFormat.AAC:
                        return ".m4a";
                    case AudioCompressionFormat.GCADPCM:
                        return ".fsb";
                    case AudioCompressionFormat.ATRAC9:
                        return ".fsb";
                }
            }
            return ".AudioClip";
        }
    }

    public static class AudioClipExtension
    {
        public static bool IsConvertSupport(this AudioClip m_AudioClip)
        {
            if (m_AudioClip.version < 5)
            {
                switch (m_AudioClip.m_Type)
                {
                    case FMODSoundType.AIFF:
                    case FMODSoundType.IT:
                    case FMODSoundType.MOD:
                    case FMODSoundType.S3M:
                    case FMODSoundType.XM:
                    case FMODSoundType.XMA:
                    case FMODSoundType.AUDIOQUEUE:
                        return true;
                    default:
                        return false;
                }
            }
            else
            {
                switch (m_AudioClip.m_CompressionFormat)
                {
                    case AudioCompressionFormat.PCM:
                    case AudioCompressionFormat.Vorbis:
                    case AudioCompressionFormat.ADPCM:
                    case AudioCompressionFormat.MP3:
                    case AudioCompressionFormat.XMA:
                        return true;
                    default:
                        return false;
                }
            }
        }

        public static bool IsLegacyConvertSupport(this AudioClip m_AudioClip)
        {
            return m_AudioClip.version < (2, 6) && m_AudioClip.m_Format != 0x05;
        }
    }
}
