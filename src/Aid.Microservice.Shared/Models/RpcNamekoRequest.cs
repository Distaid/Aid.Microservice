using System.Text.Json;

namespace Aid.Microservice.Shared.Models;

public class RpcNamekoRequest
{
    public object[] Args { get; set; } = [];
    public object? Kwargs { get; set; }

    public RpcNamekoRequest() { }

    public RpcNamekoRequest(params object[] args)
    {
        Args = args;
    }

    public RpcNamekoRequest(object[] args, object? kwargs = null)
    {
        Args = args;
        Kwargs = kwargs;
    }

    internal Dictionary<string, JsonElement>? ToParametersDictionary(JsonSerializerOptions options)
    {
        if (Kwargs == null) return null;

        using var doc = JsonSerializer.SerializeToDocument(Kwargs, options);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

        var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.Clone();
        }
        return dict;
    }
}