using System.Text.Json.Serialization;

namespace Aid.Microservice.Shared.Models;

public record RpcError
{
    public string Message { get; init; }
    public string? ErrorType { get; init; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StackTrace { get; init; }

    [JsonConstructor]
    public RpcError(string message, string? errorType = null, string? stackTrace = null)
    {
        Message = message;
        ErrorType = errorType;
        StackTrace = stackTrace;
    }
    
    public RpcError(Exception ex, bool includeStackTrace = false)
    {
        Message = ex.Message;
        ErrorType = ex.GetType().Name;
        StackTrace = includeStackTrace ? ex.StackTrace : null;
    }
}