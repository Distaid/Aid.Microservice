using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Aid.Microservice.Server.Contracts;
using Aid.Microservice.Shared.Attributes;
using Aid.Microservice.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace Aid.Microservice.Server.Infrastructure;

[RequiresUnreferencedCode("This implementation uses Reflection and is not safe for NativeAOT.")]
public class RpcEndpointRegistry(
    ILogger<RpcEndpointRegistry> logger,
    ISerializerRegistry serializerRegistry,
    IRpcProtocol protocol)
    : IRpcEndpointRegistry
{
    private readonly ConcurrentDictionary<string, Dictionary<string, RpcMethodInfo>> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, HashSet<string>> _serviceExchanges = new(StringComparer.OrdinalIgnoreCase);

    public void ScanAssemblies(Assembly assembly)
    {
        logger.LogDebug("── RPC Service Discovery ──────────────────────────");
        logger.LogDebug("Scanning assembly {Assembly} for RPC endpoints...", assembly.GetName().Name);

        var serviceTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetCustomAttribute<MicroserviceAttribute>() is not null);

        var totalMethods = 0;
        var totalServices = 0;
        foreach (var serviceType in serviceTypes)
        {
            var serviceAttr = serviceType.GetCustomAttribute<MicroserviceAttribute>()!;
            serviceAttr.SetServiceName(serviceType);
            var serviceName = serviceAttr.ServiceName;

            var methods = serviceType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<RpcCallableAttribute>() is not null);

            var methodDict = new Dictionary<string, RpcMethodInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<RpcCallableAttribute>()!;
                attr.SetMethodName(method);
                var methodName = attr.MethodName;

                if (methodDict.ContainsKey(methodName))
                {
                    logger.LogWarning("Duplicate RPC method alias '{Method}' in service '{Service}'. Ignoring overload", methodName, serviceName);
                    continue;
                }

                try
                {
                    var fastDelegate = CreateMethodDelegate(serviceType, method);

                    var serializerType = attr.SerializerType ?? serviceAttr.SerializerType;

                    methodDict[methodName] = new RpcMethodInfo(
                        ServiceType: serviceType,
                        Method: method,
                        Parameters: method.GetParameters(),
                        FastInvoke: fastDelegate,
                        SerializerType: serializerType
                    );
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to compile delegate for {Service}.{Method}", serviceName, method.Name);
                }
            }

            if (methodDict.Count > 0)
            {
                if (_endpoints.TryAdd(serviceName, methodDict))
                {
                    totalServices++;
                    totalMethods += methodDict.Count;

                    var exchanges = ResolveExchanges(serviceAttr, methodDict);
                    _serviceExchanges[serviceName] = exchanges;

                    var methodNames = methodDict
                        .Select(kvp =>
                        {
                            var alias = kvp.Key;
                            var csharpName = kvp.Value.Method.Name;
                            var serializerLabel = kvp.Value.SerializerType != null
                                ? $" [{kvp.Value.SerializerType.Name.Replace("Serializer", "")}]"
                                : "";
                            return $"{csharpName} ({alias}){serializerLabel}";
                        });

                    logger.LogDebug("  {Service,-22} → {Exchanges} ({Count} method{Plural}): {Methods}",
                        serviceName,
                        string.Join(", ", exchanges),
                        methodDict.Count,
                        methodDict.Count > 1 ? "s" : "",
                        string.Join(", ", methodNames));
                }
                else
                {
                    logger.LogWarning("Duplicate Service Name '{Service}'. Skipping implementation {Type}", serviceName, serviceType.Name);
                }
            }
        }

        logger.LogDebug("── Registered {Services} service{ServicesPlural} with {Methods} method{MethodsPlural} ──",
            totalServices,
            totalServices != 1 ? "s" : "",
            totalMethods,
            totalMethods != 1 ? "s" : "");
    }
    
    public bool TryGetMethod(string serviceName, string methodName, out RpcMethodInfo? endpointInfo)
    {
        endpointInfo = null;
        return _endpoints.TryGetValue(serviceName, out var methods) && methods.TryGetValue(methodName, out endpointInfo);
    }
    
    public IEnumerable<(string ServiceName, string ExchangeName)> GetRegisteredServiceEndpoints()
    {
        foreach (var (serviceName, exchanges) in _serviceExchanges)
        {
            foreach (var exchange in exchanges)
            {
                yield return (serviceName, exchange);
            }
        }
    }

    private HashSet<string> ResolveExchanges(MicroserviceAttribute serviceAttr, Dictionary<string, RpcMethodInfo> methods)
    {
        var exchanges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (serviceAttr.Exchanges is { Length: > 0 })
        {
            foreach (var ex in serviceAttr.Exchanges)
            {
                if (!string.IsNullOrWhiteSpace(ex))
                {
                    exchanges.Add(ex);
                }
            }
        }

        if (exchanges.Count == 0 && !string.IsNullOrWhiteSpace(serviceAttr.ExchangeName))
        {
            exchanges.Add(serviceAttr.ExchangeName!);
        }

        if (exchanges.Count == 0)
        {
            foreach (var method in methods.Values)
            {
                var exchangeName = method.SerializerType != null
                    ? serializerRegistry.GetSerializer(method.SerializerType)?.ExchangeName
                    : protocol.DefaultSerializer.ExchangeName;

                if (!string.IsNullOrWhiteSpace(exchangeName))
                {
                    exchanges.Add(exchangeName);
                }
            }
        }

        if (exchanges.Count == 0)
        {
            exchanges.Add(protocol.DefaultSerializer.ExchangeName ?? "aid_rpc");
        }

        return exchanges;
    }
    
    private static Func<object, object?[], Task<object?>> CreateMethodDelegate(Type serviceType, MethodInfo method)
    {
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var argsParam = Expression.Parameter(typeof(object?[]), "args");

        var typedInstance = Expression.Convert(instanceParam, serviceType);

        var parameters = method.GetParameters();
        var callArgs = new Expression[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var argIndex = Expression.Constant(i);
            var argAccess = Expression.ArrayIndex(argsParam, argIndex);
            callArgs[i] = Expression.Convert(argAccess, parameters[i].ParameterType);
        }

        var call = Expression.Call(typedInstance, method, callArgs);

        Expression resultExpression;

        if (method.ReturnType == typeof(void))
        {
            // void: return Task.FromResult<object?>(null)
            var nullTask = Expression.Call(
                typeof(Task), 
                nameof(Task.FromResult), 
                [typeof(object)], 
                Expression.Constant(null)
            );
            
            resultExpression = Expression.Block(call, nullTask);
        }
        else if (method.ReturnType == typeof(Task))
        {
            // Task: await task; return null;
            resultExpression = Expression.Call(
                typeof(RpcEndpointRegistry),
                nameof(WrapVoidTaskAsync),
                Type.EmptyTypes,
                call
            );
        }
        else if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            // Task<T>: return (object) await task;
            var resultType = method.ReturnType.GetGenericArguments()[0];
            resultExpression = Expression.Call(
                typeof(RpcEndpointRegistry),
                nameof(WrapGenericTaskAsync),
                [resultType],
                call
            );
        }
        else if (method.ReturnType == typeof(ValueTask))
        {
            // ValueTask: await task; return null;
            var asTaskMethod = typeof(ValueTask).GetMethod(nameof(ValueTask.AsTask))!;
            var taskCall = Expression.Call(call, asTaskMethod);
            resultExpression = Expression.Call(
                typeof(RpcEndpointRegistry),
                nameof(WrapVoidTaskAsync),
                Type.EmptyTypes,
                taskCall
            );
        }
        else if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            // ValueTask<T>: return (object) await task;
            var resultType = method.ReturnType.GetGenericArguments()[0];
            var valueTaskType = typeof(ValueTask<>).MakeGenericType(resultType);
            var asTaskMethod = valueTaskType.GetMethod(nameof(ValueTask<object>.AsTask))!;
            var taskCall = Expression.Call(call, asTaskMethod);
            resultExpression = Expression.Call(
                typeof(RpcEndpointRegistry),
                nameof(WrapGenericTaskAsync),
                [resultType],
                taskCall
            );
        }
        else
        {
            //T: return Task.FromResult((object)result)
            var castResult = Expression.Convert(call, typeof(object));
            resultExpression = Expression.Call(
                typeof(Task),
                nameof(Task.FromResult),
                [typeof(object)],
                castResult
            );
        }

        var lambda = Expression.Lambda<Func<object, object?[], Task<object?>>>(
            resultExpression, instanceParam, argsParam
        );

        return lambda.Compile();
    }
    
    private static Task<object?> WrapVoidTaskAsync(Task task)
    {
        if (task.Status == TaskStatus.RanToCompletion)
        {
            return Task.FromResult<object?>(null);
        }
        
        return task.ContinueWith(
            t => 
            {
                t.GetAwaiter().GetResult();
                return (object?)null; 
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }

    private static Task<object?> WrapGenericTaskAsync<T>(Task<T> task)
    {
        if (task.Status == TaskStatus.RanToCompletion)
        {
            return Task.FromResult((object?)task.Result);
        }

        return task.ContinueWith(
            t => (object?)t.GetAwaiter().GetResult(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }
}