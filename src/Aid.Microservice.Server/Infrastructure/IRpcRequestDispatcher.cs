using System.Text.Json;
using Aid.Microservice.Shared.Models;

namespace Aid.Microservice.Server.Infrastructure;

public interface IRpcRequestDispatcher
{
    Task<RpcResponse> DispatchAsync(string serviceName, string methodName, Dictionary<string, JsonElement>? parameters);
}