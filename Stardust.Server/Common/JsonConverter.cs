using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stardust.Server.Common
{
    /// <summary>Json反序列化时进行类型绑定</summary>
    /// <typeparam name="TService"></typeparam>
    /// <typeparam name="TImplementation"></typeparam>
    public class JsonConverter<TService, TImplementation> : JsonConverter<TService> where TImplementation : TService
    {
        public override TService Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => JsonSerializer.Deserialize<TImplementation>(ref reader, options);

        public override void Write(Utf8JsonWriter writer, TService value, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, value, options);
    }
}