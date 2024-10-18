using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetStudio
{
    public sealed class Texture2DArray : Texture
    {
        public int m_Width;
        public int m_Height;
        public int m_Depth;
        public GraphicsFormat m_Format;
        public int m_MipCount;
        public uint m_DataSize;
        public GLTextureSettings m_TextureSettings;
        public int m_ColorSpace;
        [JsonPropertyName("image data")]
        public ResourceReader image_data;
        public StreamingInfo m_StreamData;
        public List<Texture2D> TextureList;

        public Texture2DArray() { }

        public Texture2DArray(ObjectReader reader) : base(reader)
        {
            m_ColorSpace = reader.ReadInt32();
            m_Format = (GraphicsFormat)reader.ReadInt32();
            m_Width = reader.ReadInt32();
            m_Height = reader.ReadInt32();
            m_Depth = reader.ReadInt32();
            m_MipCount = reader.ReadInt32();
            m_DataSize = reader.ReadUInt32();
            m_TextureSettings = new GLTextureSettings(reader);
            if (version >= (2020, 2)) //2020.2 and up
            {
                var m_UsageMode = reader.ReadInt32();
            }
            var m_IsReadable = reader.ReadBoolean();
            reader.AlignStream(); 

            var image_data_size = reader.ReadInt32();
            if (image_data_size == 0)
            {
                m_StreamData = new StreamingInfo(reader);
            }

            image_data = !string.IsNullOrEmpty(m_StreamData?.path)
                ? new ResourceReader(m_StreamData.path, assetsFile, m_StreamData.offset, (int)m_StreamData.size)
                : new ResourceReader(reader, reader.BaseStream.Position, image_data_size);

            TextureList = new List<Texture2D>();
        }

        public Texture2DArray(ObjectReader reader, byte[] type, JsonSerializerOptions jsonOptions) : base(reader)
        {
            var parsedTex2dArray = JsonSerializer.Deserialize<Texture2DArray>(type, jsonOptions);
            m_Width = parsedTex2dArray.m_Width;
            m_Height = parsedTex2dArray.m_Height;
            m_Depth = parsedTex2dArray.m_Depth;
            m_Format = parsedTex2dArray.m_Format;
            m_MipCount = parsedTex2dArray.m_MipCount;
            m_DataSize = parsedTex2dArray.m_DataSize;
            m_TextureSettings = parsedTex2dArray.m_TextureSettings;
            m_StreamData = parsedTex2dArray.m_StreamData;
            
            image_data = !string.IsNullOrEmpty(m_StreamData?.path)
                ? new ResourceReader(m_StreamData.path, assetsFile, m_StreamData.offset, m_StreamData.size)
                : new ResourceReader(reader, parsedTex2dArray.image_data.Offset, parsedTex2dArray.image_data.Size);

            TextureList = new List<Texture2D>();
        }
    }
}
