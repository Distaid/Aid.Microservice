using Aid.Microservice.Shared.Models;

namespace Aid.Microservice.Client.Models;

/// <summary>
/// Exception thrown when an RPC call fails, either on the server side or due to a client-side error.
/// </summary>
public class RpcCallException : Exception
{
    /// <summary>
    /// The correlation ID of the failed RPC call.
    /// </summary>
    public string? CorrelationId { get; }

    /// <summary>
    /// Server-side error details, if the error originated from the server.
    /// </summary>
    public RpcError? RpcError { get; }

    /// <summary>
    /// Creates an instance from a server-side <see cref="RpcError"/>.
    /// </summary>
    public RpcCallException(RpcError error, string? correlationId)
        : base($"RPC call failed on server (CorrelationId: {correlationId}). ErrorType: '{error.ErrorType}', Message: '{error.Message}'")
    {
        RpcError = error;
        CorrelationId = correlationId;
    }

    /// <summary>
    /// Creates an instance for a client-side error.
    /// </summary>
    public RpcCallException(string message, string? correlationId, Exception? innerException = null)
        : base($"RPC client error (CorrelationId: {correlationId}). Message: {message}", innerException)
    {
        RpcError = null;
        CorrelationId = correlationId;
    }
}