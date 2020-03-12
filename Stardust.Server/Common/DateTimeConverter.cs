using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Stardust.Server.Common
{
    public class DateTimeConverter : JsonConverter<DateTime>
    {
        public String DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => DateTime.Parse(reader.GetString());

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString(DateTimeFormat));
    }
}
