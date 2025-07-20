namespace Aid.Microservice.Shared.Attributes;

/// <summary>
/// Attribute that mark class as microservice.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class MicroserviceAttribute : Attribute
{
    private readonly string? _alias;

    public string ServiceName { get; private set; } = string.Empty;

    /// <summary>
    /// Mark class as microservice.
    /// </summary>
    /// <param name="alias">Name for service. Used name of class as default</param>
    public MicroserviceAttribute(string? alias = null)
    {
        _alias = alias?.Trim().ToLowerInvariant();
    }

    public string QueueName => $"rpc_aid_{ServiceName}";

    public void SetServiceName(Type serviceType)
    {
        if (!string.IsNullOrEmpty(_alias))
        {
            ServiceName = _alias;
        }
        else
        {
            ServiceName = serviceType.Name.ToLowerInvariant();

            if (ServiceName.EndsWith("service", StringComparison.OrdinalIgnoreCase))
            {
                ServiceName = ServiceName.Replace("service", string.Empty);
            }

            if (string.IsNullOrEmpty(ServiceName))
            {
                ServiceName = serviceType.Name.ToLowerInvariant();
            }
        }
    }
}