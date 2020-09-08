using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewLife.Log;

namespace Stardust.Server.Common
{
    internal class JsonConverterForBuilder : JsonConverter<ISpanBuilder>
    {
        public override ISpanBuilder Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => JsonSerializer.Deserialize<DefaultSpanBuilder>(ref reader, options);

        public override void Write(Utf8JsonWriter writer, ISpanBuilder value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, options);
    }

    internal class JsonConverterForSpan : JsonConverter<ISpan>
    {
        public override ISpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => JsonSerializer.Deserialize<DefaultSpan>(ref reader, options);

        public override void Write(Utf8JsonWriter writer, ISpan value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, options);
    }
}