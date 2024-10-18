using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetStudio
{
    public static partial class JsonConverterHelper
    {
        public class ByteArrayConverter : JsonConverter<byte[]>
        {
            public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return reader.TokenType == JsonTokenType.StartArray
                    ? JsonSerializer.Deserialize<List<byte>>(ref reader).ToArray() //JsonArray to ByteArray
                    : JsonSerializer.Deserialize<byte[]>(ref reader);
            }

            public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
            {
                writer.WriteBase64StringValue(value);
            }
        }
    }
}
