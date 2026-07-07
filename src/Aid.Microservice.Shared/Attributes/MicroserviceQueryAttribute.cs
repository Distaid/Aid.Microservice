using System;

namespace Aid.Microservice.Shared.Attributes;

/// <summary>
/// Attribute that marks a class as a single-endpoint Microservice Query/Command handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class MicroserviceQueryAttribute : Attribute
{
    private readonly string? _alias;

    public string QueryName { get; private set; } = string.Empty;

    /// <summary>
    /// Custom serializer type for this query.
    /// Must implement <see cref="Shared.Interfaces.IRequestSerializer"/>.
    /// </summary>
    public Type? SerializerType { get; init; }

    /// <summary>
    /// Explicit exchange name for this query.
    /// </summary>
    public string? ExchangeName { get; init; }

    /// <summary>
    /// Mark class as microservice query.
    /// </summary>
    /// <param name="alias">Name for the query. Inferred from class name if not specified</param>
    public MicroserviceQueryAttribute(string? alias = null)
    {
        _alias = alias?.Trim().ToLowerInvariant();
    }

    public void SetQueryName(Type queryType)
    {
        if (!string.IsNullOrEmpty(_alias))
        {
            QueryName = _alias;
        }
        else
        {
            var name = queryType.Name.ToLowerInvariant();

            // Truncate suffixes in descending order of length or specific order
            var suffixes = new[] { "queryhandler", "commandhandler", "query", "command", "handler" };
            foreach (var suffix in suffixes)
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name[..^suffix.Length];
                    break;
                }
            }

            if (string.IsNullOrEmpty(name))
            {
                name = queryType.Name.ToLowerInvariant();
            }

            QueryName = name;
        }
    }
}
