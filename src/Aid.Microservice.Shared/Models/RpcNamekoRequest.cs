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
}