using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Aid.Microservice.Server.Contracts;
using Aid.Microservice.Shared.Attributes;
using Microsoft.Extensions.Logging;

namespace Aid.Microservice.Server.Infrastructure;

public class RpcEndpointRegistry(ILogger<RpcEndpointRegistry> logger) : IRpcEndpointRegistry
{
    private readonly ConcurrentDictionary<string, Dictionary<string, RpcMethodInfo>> _endpoints = new(StringComparer.OrdinalIgnoreCase);

    public void ScanAssemblies(Assembly assembly)
    {
        logger.LogInformation("Scanning assembly {Assembly} for RPC endpoints...", assembly.GetName().Name);

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
                    
                    methodDict[methodName] = new RpcMethodInfo(
                        ServiceType: serviceType,
                        Method: method,
                        Parameters: method.GetParameters(),
                        FastInvoke: fastDelegate
                    );
                    
                    logger.LogDebug("Registered RPC method: {Service}.{Method}", serviceName, methodName);
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
                }
                else
                {
                    logger.LogWarning("Duplicate Service Name '{Service}'. Skipping implementation {Type}", serviceName, serviceType.Name);
                }
            }
        }
        
        logger.LogInformation("RPC Discovery Complete. Registered {Services} services with {Methods} methods", totalServices, totalMethods);
    }
    
    public bool TryGetMethod(string serviceName, string methodName, out RpcMethodInfo? endpointInfo)
    {
        endpointInfo = null;
        return _endpoints.TryGetValue(serviceName, out var methods) && methods.TryGetValue(methodName, out endpointInfo);
    }
    
    public IEnumerable<string> GetRegisteredServices() => _endpoints.Keys;
    
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