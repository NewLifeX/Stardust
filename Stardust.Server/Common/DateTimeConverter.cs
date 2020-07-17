using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewLife;

namespace Stardust.Server.Common
{
    public class DateTimeConverter : JsonConverter<DateTime>
    {
        public String DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            var utc = false;
            if (str.EndsWith("UTC"))
            {
                str = str.TrimEnd("UTC").Trim();
                utc = true;
            }
            if (!DateTime.TryParse(str, out var dt)) return DateTime.MinValue;

            if (utc) dt = dt.ToLocalTime();

            return dt;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString(DateTimeFormat));
    }
}
