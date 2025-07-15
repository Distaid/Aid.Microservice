namespace Aid.Microservice.Shared.Models;

public class RpcResponse
{
    public object? Result { get; set; }
    public RpcError? Error { get; set; }

    public bool IsSuccess => Error == null;
}