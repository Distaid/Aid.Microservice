using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aid.Microservice.Shared.Converters;

public class CaseInsensitiveDictionaryConverter : JsonConverter<Dictionary<string, JsonElement>>
{
    public override Dictionary<string, JsonElement>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }
        
        var dictionary = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException();
            }
            var propertyName = reader.GetString()!;
            
            reader.Read();
            var value = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
            
            dictionary[propertyName] = value;
        }
        return dictionary;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, JsonElement> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }
}