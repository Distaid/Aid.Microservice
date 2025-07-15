using Aid.Microservice.Shared.Models;

namespace Aid.Microservice.Client.Models;

public class RpcCallException : Exception
{
    public string? CorrelationId { get; }
    public RpcError? RpcError { get; }

    public RpcCallException(RpcError error, string? correlationId)
        : base($"RPC call failed on server (CorrelationId: {correlationId}). ErrorType: '{error.ErrorType}', Message: '{error.Message}'")
    {
        RpcError = error;
        CorrelationId = correlationId;
    }
    
    public RpcCallException(string message, string? correlationId, Exception? innerException = null)
        : base($"RPC client error (CorrelationId: {correlationId}). Message: {message}", innerException)
    {
        RpcError = null;
        CorrelationId = correlationId;
    }
}