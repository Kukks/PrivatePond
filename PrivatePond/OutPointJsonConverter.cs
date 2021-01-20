using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NBitcoin;

namespace PrivatePond
{
    public class OutPointJsonConverter:JsonConverter<OutPoint>
    {
        public override OutPoint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var raw = reader.GetString();
            return raw is null? null: OutPoint.Parse(raw);
        }

        public override void Write(Utf8JsonWriter writer, OutPoint value, JsonSerializerOptions options)
        {
            if (value is not null)
            {
                writer.WriteStringValue(value.ToString() );
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}