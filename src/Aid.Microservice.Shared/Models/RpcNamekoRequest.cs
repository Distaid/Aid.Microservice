using System.Text.Json;

namespace Aid.Microservice.Shared.Models;

/// <summary>
/// Wrapper for sending positional arguments (<c>args</c>) alongside named arguments (<c>kwargs</c>)
/// to Nameko-compatible services.
/// </summary>
/// <example>
/// <code>
/// // args: [10, 20]
/// await client.CallAsync("sum", new RpcNamekoRequest(10, 20));
///
/// // args=['pdf'], kwargs={'async': true}
/// await client.CallAsync("generate",
///     new RpcNamekoRequest(
///         args: new object[] { "pdf" },
///         kwargs: new { async = true }
///     ));
/// </code>
/// </example>
public class RpcNamekoRequest
{
    /// <summary>
    /// Positional arguments to send.
    /// </summary>
    public object[] Args { get; set; } = [];

    /// <summary>
    /// Named arguments to send alongside positional ones.
    /// </summary>
    public object? Kwargs { get; set; }

    /// <summary>
    /// Creates an empty request.
    /// </summary>
    public RpcNamekoRequest() { }

    /// <summary>
    /// Creates a request with positional arguments only.
    /// </summary>
    public RpcNamekoRequest(params object[] args)
    {
        Args = args;
    }

    /// <summary>
    /// Creates a request with both positional and named arguments.
    /// </summary>
    public RpcNamekoRequest(object[] args, object? kwargs = null)
    {
        Args = args;
        Kwargs = kwargs;
    }

    /// <summary>
    /// Converts the <see cref="Kwargs"/> to a dictionary of <see cref="JsonElement"/> values.
    /// Returns <c>null</c> if <see cref="Kwargs"/> is not set.
    /// </summary>
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