using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetStudio
{
    public static partial class JsonConverterHelper
    {
        public class PPtrConverter : JsonConverterFactory
        {
            public static SerializedFile AssetsFile;

            public override bool CanConvert(Type typeToConvert)
            {
                if (!typeToConvert.IsGenericType)
                    return false;

                var generic = typeToConvert.GetGenericTypeDefinition();
                return generic == typeof(PPtr<>);
            }

            public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
            {
                var elementType = type.GetGenericArguments()[0];
                var converter = (JsonConverter)Activator.CreateInstance(typeof(PPtrConverter<>).MakeGenericType(elementType), AssetsFile);
                return converter;
            }
        }

        private class PPtrConverter<T> : JsonConverter<PPtr<T>> where T : Object
        {
            private readonly SerializedFile _assetsFile;

            public PPtrConverter(SerializedFile assetsFile)
            {
                _assetsFile = assetsFile;
            }

            public override PPtr<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var pptrObj = JsonSerializer.Deserialize<PPtr<T>>(ref reader, new JsonSerializerOptions { IncludeFields = true });
                pptrObj.SetAssetsFile(_assetsFile);
                return pptrObj;
            }

            public override void Write(Utf8JsonWriter writer, PPtr<T> value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(value);
            }
        }
    }
}
