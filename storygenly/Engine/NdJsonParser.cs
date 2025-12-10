using System.Text.Json;

namespace StoryGenly.Engine
{
    public class NdJsonParser
    {
        public static void WriteToFile(string filePath, IEnumerable<JsonElement> elements)
        {
            using var writer = new StreamWriter(filePath);
            foreach (var element in elements)
            {
                writer.WriteLine(element.GetRawText());
            }
        }   

        public static IEnumerable<JsonElement> Parse(string json)
        {
            using StringReader reader = new StringReader(json);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (!TryParse(trimmed, out JsonElement jsonElement))
                {
                    Serilog.Log.Error("Failed to parse line as JSON: {Line}", trimmed);
                    continue;
                }

                yield return jsonElement;
            }
        }

        public static bool TryParse(string json, out JsonElement result)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                result = doc.RootElement;
                return true;
            }
            catch (JsonException)
            {
                result = default;
                return false;
            }
        }
        
        public static string ToNdJsonString(IEnumerable<JsonElement> elements)
        {
            return string.Join('\n', elements.Select(element => element.GetRawText()));
        }

        public static string? GetStringProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        public static int? GetIntProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
                ? property.GetInt32()
                : null;
        }

        public static bool? GetBoolProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
                ? property.GetBoolean()
                : null;
        }

        public static JsonElement? GetNestedProperty(JsonElement element, string propertyPath)
        {
            var properties = propertyPath.Split('.');
            var current = element;
            foreach (var property in properties)
            {
                if (!current.TryGetProperty(property, out current))
                    return null;
            }
            return current;
        }

        public static string? GetNestedStringProperty(JsonElement element, string propertyPath)
        {
            var nested = GetNestedProperty(element, propertyPath);
            return nested?.ValueKind == JsonValueKind.String ? nested.Value.GetString() : null;
        }

        public static IEnumerable<JsonElement> GetArrayProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array)
            {
                return property.EnumerateArray();
            }
            return Enumerable.Empty<JsonElement>();
        }

        public static IEnumerable<string> GetStringArrayProperty(JsonElement element, string propertyName)
        {
            return GetArrayProperty(element, propertyName)
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .Where(str => str != null);
        }

        public static bool HasProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out _);
        }
    }
}