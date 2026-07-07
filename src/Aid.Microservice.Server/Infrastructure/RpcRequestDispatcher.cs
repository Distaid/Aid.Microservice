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

            var paramStr = parameters != null
                ? JsonSerializer.Serialize(parameters, _jsonOptions)
                : "none";

            logger.LogInformation("Invoking {Service}.{Method} with parameters: {Parameters}", serviceName, methodName, paramStr);

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

        var payloadParams = methodParams.Where(p => p.ParameterType != typeof(CancellationToken)).ToList();

        // If there is exactly one complex parameter (e.g. CQRS Request object), bind the whole input dictionary to it.
        if (payloadParams.Count == 1 && IsComplexType(payloadParams[0].ParameterType))
        {
            var targetParam = payloadParams[0];
            object? deserializedValue = null;
            if (inputParams != null && inputParams.Count > 0)
            {
                try
                {
                    var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(inputParams, _jsonOptions);
                    deserializedValue = JsonSerializer.Deserialize(jsonBytes, targetParam.ParameterType, _jsonOptions);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Failed to deserialize complex request parameter '{targetParam.Name}'. Expected: {targetParam.ParameterType.Name}. Error: {ex.Message}");
                }
            }

            var args = new object?[methodParams.Length];
            for (var i = 0; i < methodParams.Length; i++)
            {
                var paramInfo = methodParams[i];
                if (paramInfo.ParameterType == typeof(CancellationToken))
                {
                    args[i] = CancellationToken.None;
                }
                else
                {
                    args[i] = deserializedValue;
                }
            }
            return args;
        }

        var resArgs = new object?[methodParams.Length];

        for (var i = 0; i < methodParams.Length; i++)
        {
            var paramInfo = methodParams[i];
            
            if (paramInfo.ParameterType == typeof(CancellationToken))
            {
                resArgs[i] = CancellationToken.None;
                continue;
            }

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
                    resArgs[i] = null;
                }
                else
                {
                    try
                    {
                        resArgs[i] = jsonValue.Value.Deserialize(paramInfo.ParameterType, _jsonOptions);
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
                    resArgs[i] = paramInfo.DefaultValue;
                }
                else if (IsNullable(paramInfo))
                {
                    resArgs[i] = null;
                }
                else
                {
                    throw new ArgumentException($"Missing required parameter '{paramName}'");
                }
            }
        }
        
        return resArgs;
    }

    private static bool IsComplexType(Type type)
    {
        if (type.IsPrimitive || type == typeof(string) || type.IsEnum || type == typeof(decimal) || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(DateTimeOffset))
        {
            return false;
        }
        
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            return IsComplexType(underlying);
        }

        return true;
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