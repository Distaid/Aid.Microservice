using System.Reflection;
using System.Text.Json;
using Aid.Microservice.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aid.Microservice.Server.Infrastructure;

public class RpcRequestDispatcher(
    IRpcEndpointRegistry registry,
    IServiceProvider serviceProvider,
    ILogger<RpcRequestDispatcher> logger)
    : IRpcRequestDispatcher
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null
    };

    public async Task<RpcResponse> DispatchAsync(string serviceName, string methodName, Dictionary<string, JsonElement>? parameters)
    {
        object? callResult = null;
        RpcError? callError = null;

        try
        {
            if (!registry.TryGetMethod(serviceName, methodName, out var endpointInfo) || endpointInfo == null)
            {
                throw new MissingMethodException($"Method '{methodName}' not found in service '{serviceName}'");
            }
            
            using var scope = serviceProvider.CreateScope();
            var serviceInstance = scope.ServiceProvider.GetService(endpointInfo.ServiceType);
            
            if (serviceInstance == null)
            {
                throw new InvalidOperationException($"Could not resolve service type '{endpointInfo.ServiceType.Name}'");
            }
            
            var arguments = PrepareArguments(endpointInfo.Parameters, parameters);
            
            callResult = await endpointInfo.FastInvoke(serviceInstance, arguments);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing RPC method {Service}.{Method}", serviceName, methodName);
            
            var actualException = ex is TargetInvocationException && ex.InnerException != null 
                ? ex.InnerException 
                : ex;

            callError = new RpcError(actualException, includeStackTrace: logger.IsEnabled(LogLevel.Debug));
        }
        
        return new RpcResponse 
        { 
            Result = callResult, 
            Error = callError 
        };
    }

    private object?[] PrepareArguments(ParameterInfo[] methodParams, Dictionary<string, JsonElement>? inputParams)
    {
        if (methodParams.Length == 0)
        {
            return [];
        }

        var args = new object?[methodParams.Length];

        for (var i = 0; i < methodParams.Length; i++)
        {
            var paramInfo = methodParams[i];
            var paramName = paramInfo.Name!;
            
            JsonElement? jsonValue = null;
            
            if (inputParams != null && inputParams.TryGetValue(paramName, out var paramValue))
            {
                jsonValue = paramValue;
            }
            
            if (jsonValue.HasValue)
            {
                if (jsonValue.Value.ValueKind == JsonValueKind.Null)
                {
                    args[i] = null;
                }
                else
                {
                    try
                    {
                        args[i] = jsonValue.Value.Deserialize(paramInfo.ParameterType, _jsonOptions);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException($"Failed to deserialize parameter '{paramName}'. Expected: {paramInfo.ParameterType.Name}. Error: {ex.Message}");
                    }
                }
            }
            else
            {
                if (paramInfo.HasDefaultValue)
                {
                    args[i] = paramInfo.DefaultValue;
                }
                else if (IsNullable(paramInfo))
                {
                    args[i] = null;
                }
                else
                {
                    throw new ArgumentException($"Missing required parameter '{paramName}'");
                }
            }
        }
        
        return args;
    }

    private static bool IsNullable(ParameterInfo param)
    {
        if (!param.ParameterType.IsValueType)
        {
            return true;
        }
        
        return Nullable.GetUnderlyingType(param.ParameterType) != null;
    }
}