using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AssetStudio
{
    public static partial class JsonConverterHelper
    {
        public class RenderDataMapConverter : JsonConverter<Dictionary<KeyValuePair<Guid, long>, SpriteAtlasData>>
        {
            public override Dictionary<KeyValuePair<Guid, long>, SpriteAtlasData> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var dataArray = JsonSerializer.Deserialize<KeyValuePair<JsonObject, SpriteAtlasData>[]>(ref reader, options);
                var renderDataMap = new Dictionary<KeyValuePair<Guid, long>, SpriteAtlasData>(dataArray.Length);
                foreach (var kvp in dataArray)
                {
                    var jsonFirst = kvp.Key["first"];
                    var first = jsonFirst.Deserialize<GUID>(options).Convert();
                    var second = (long) kvp.Key["second"];
                    renderDataMap.Add(new KeyValuePair<Guid, long>(first, second), kvp.Value);
                }
                return renderDataMap;
            }

            public override void Write(Utf8JsonWriter writer, Dictionary<KeyValuePair<Guid, long>, SpriteAtlasData> value, JsonSerializerOptions options)
            {
                var jsonDict = new Dictionary<string, SpriteAtlasData>();
                foreach (var kv in value)
                {
                    var strKey = $"{kv.Key.Key}, {kv.Key.Value}";
                    jsonDict.Add(strKey, kv.Value);
                }
                var strValue = JsonSerializer.Serialize(jsonDict, options).Replace("  ", "    ");
                writer.WriteRawValue(strValue);
            }
        }

        private class GUID
        {
            [JsonPropertyName("data[0]")] public uint data0;
            [JsonPropertyName("data[1]")] public uint data1;
            [JsonPropertyName("data[2]")] public uint data2;
            [JsonPropertyName("data[3]")] public uint data3;

            public Guid Convert()
            {
                var guidBytes = new byte[16];
                BitConverter.GetBytes(data0).CopyTo(guidBytes, 0);
                BitConverter.GetBytes(data1).CopyTo(guidBytes, 4);
                BitConverter.GetBytes(data2).CopyTo(guidBytes, 8);
                BitConverter.GetBytes(data3).CopyTo(guidBytes, 12);
                return new Guid(guidBytes);
            }
        }
    }
}
