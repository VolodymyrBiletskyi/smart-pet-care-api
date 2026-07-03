using System.Text.Json;
using System.Text.Json.Serialization;

namespace smart_pet_care_api.Common.Patching
{
    public sealed class PatchFieldJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(PatchField<>);

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var valueType = typeToConvert.GetGenericArguments()[0];
            var converterType = typeof(PatchFieldJsonConverter<>).MakeGenericType(valueType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

        private sealed class PatchFieldJsonConverter<T> : JsonConverter<PatchField<T>>
        {
            public override PatchField<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var value = JsonSerializer.Deserialize<T>(ref reader, options);
                return PatchField<T>.Set(value);
            }

            public override void Write(Utf8JsonWriter writer, PatchField<T> value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value.Value, options);
            }
        }
    }
}
