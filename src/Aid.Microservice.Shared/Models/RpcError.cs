namespace Aid.Microservice.Shared.Models;

public class RpcError
{
    public string Message { get; set; } = null!;
    public string? StackTrace { get; set; }
    public string? ErrorType { get; set; }

    public RpcError() { }

    public RpcError(string message, string? stackTrace = null, string? errorType = null)
    {
        Message = message;
        StackTrace = stackTrace;
        ErrorType = errorType;
    }

    public RpcError(Exception ex, bool includeStackTrace = false)
    {
        Message = ex.Message;
        ErrorType = ex.GetType().Name;

        if (includeStackTrace)
        {
            StackTrace = ex.StackTrace;
        }
    }

}