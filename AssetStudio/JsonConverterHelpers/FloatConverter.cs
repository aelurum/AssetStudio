using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssetStudio
{
    public static partial class JsonConverterHelper
    {
        public class FloatConverter : JsonConverter<float>
        {
            public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return JsonSerializer.Deserialize<float>(ref reader, new JsonSerializerOptions
                {
                    NumberHandling = options.NumberHandling
                });
            }

            public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    if (options.NumberHandling == JsonNumberHandling.AllowNamedFloatingPointLiterals)
                    {
                        writer.WriteStringValue($"{value.ToString(CultureInfo.InvariantCulture)}");
                    }
                    else
                    {
                        writer.WriteStringValue(JsonSerializer.Serialize(value));
                    }
                }
                else
                {
                    writer.WriteNumberValue((decimal)value);
                }
            }
        }
    }
}
