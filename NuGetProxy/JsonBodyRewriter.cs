using System.Text.Json;

namespace NuGetProxy;

public static class JsonBodyRewriter
{
    public static async Task ReplaceStringInJsonAsync(Stream jsonStream, Stream outStream, string findValue, string replaceValue, CancellationToken cancellationToken = default)
    {
        await using var writer = new Utf8JsonWriter(outStream);
        var doc = await JsonDocument.ParseAsync(jsonStream, default, cancellationToken);

        RecurseJson(doc.RootElement, writer, findValue, replaceValue, cancellationToken);
    }

    private static void RecurseJson(JsonElement element, Utf8JsonWriter writer, string findValue, string replaceValue, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    RecurseJson(property.Value, writer, findValue, replaceValue, cancellationToken);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    RecurseJson(item, writer, findValue, replaceValue, cancellationToken);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                var value = element.GetString();
                writer.WriteStringValue(
                    (value?.StartsWith(findValue, StringComparison.OrdinalIgnoreCase) ?? false)
                    ? value.Replace(findValue, replaceValue, StringComparison.OrdinalIgnoreCase)
                    : value
                );
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}