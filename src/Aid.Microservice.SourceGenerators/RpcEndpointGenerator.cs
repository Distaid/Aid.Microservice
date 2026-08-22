using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Aid.Microservice.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Aid.Microservice.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public class RpcEndpointGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, ct) => GetServiceModel(ctx, ct))
            .Where(static m => m is not null);

        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) =>
        {
            var (compilation, services) = source;

            // Only generate server endpoints if this project references Aid.Microservice.Server
            var serverSymbol = compilation.GetTypeByMetadataName("Aid.Microservice.Server.Infrastructure.IRpcEndpointRegistry");
            if (serverSymbol == null)
            {
                return;
            }

            if (services.IsDefaultOrEmpty)
            {
                return;
            }

            var validServices = services.Where(s => s is not null).Cast<ServiceModel>().ToList();
            if (validServices.Count == 0)
            {
                return;
            }

            GenerateSources(spc, validServices);
        });
    }

    private static ServiceModel? GetServiceModel(GeneratorSyntaxContext context, System.Threading.CancellationToken ct)
    {
        if (context.Node is not ClassDeclarationSyntax classSyntax) return null;

        var symbol = context.SemanticModel.GetDeclaredSymbol(classSyntax, ct);
        if (symbol is null || symbol.IsAbstract) return null;

        var microserviceAttr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is "MicroserviceAttribute" or "Microservice");

        var microserviceQueryAttr = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name is "MicroserviceQueryAttribute" or "MicroserviceQuery");

        if (microserviceAttr is null && microserviceQueryAttr is null)
        {
            return null;
        }

        var classFullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (microserviceAttr != null)
        {
            var serviceName = GetServiceName(symbol, microserviceAttr);
            var (customSerializer, exchangeName, exchanges) = ParseServiceAttributeProperties(microserviceAttr);

            var methods = new List<MethodModel>();
            var methodSymbols = symbol.GetMembers().OfType<IMethodSymbol>()
                .Where(m => m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic);

            foreach (var method in methodSymbols)
            {
                var rpcAttr = method.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name is "RpcCallableAttribute" or "RpcCallable");

                if (rpcAttr is null) continue;

                var methodAlias = GetMethodAlias(method, rpcAttr);
                var methodSerializer = GetSerializerTypeFromAttr(rpcAttr) ?? customSerializer;
                var (returnKind, returnTypeFullName) = AnalyzeReturnType(method.ReturnType);
                var parameters = AnalyzeParameters(method.Parameters);

                methods.Add(new MethodModel(
                    methodName: method.Name,
                    rpcAlias: methodAlias,
                    customSerializerTypeFullName: methodSerializer,
                    returnTypeKind: returnKind,
                    returnTypeFullName: returnTypeFullName,
                    parameters: parameters
                ));
            }

            if (methods.Count == 0) return null;

            return new ServiceModel(
                classFullName: classFullName,
                serviceName: serviceName,
                customSerializerTypeFullName: customSerializer,
                explicitExchangeName: exchangeName,
                explicitExchanges: exchanges,
                methods: methods,
                isQueryHandler: false,
                queryName: null
            );
        }

        if (microserviceQueryAttr != null)
        {
            var queryName = GetQueryName(symbol, microserviceQueryAttr);
            var (customSerializer, exchangeName, exchanges) = ParseServiceAttributeProperties(microserviceQueryAttr);

            var handleMethod = symbol.GetMembers().OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.DeclaredAccessibility == Accessibility.Public &&
                                     !m.IsStatic &&
                                     (m.Name.Equals("Handle", StringComparison.OrdinalIgnoreCase) ||
                                      m.Name.Equals("HandleAsync", StringComparison.OrdinalIgnoreCase)));

            if (handleMethod is null) return null;

            var (returnKind, returnTypeFullName) = AnalyzeReturnType(handleMethod.ReturnType);
            var parameters = AnalyzeParameters(handleMethod.Parameters);

            var methods = new List<MethodModel>
            {
                new (
                    methodName: handleMethod.Name,
                    rpcAlias: "handle",
                    customSerializerTypeFullName: customSerializer,
                    returnTypeKind: returnKind,
                    returnTypeFullName: returnTypeFullName,
                    parameters: parameters
                ),
                new (
                    methodName: handleMethod.Name,
                    rpcAlias: "handleasync",
                    customSerializerTypeFullName: customSerializer,
                    returnTypeKind: returnKind,
                    returnTypeFullName: returnTypeFullName,
                    parameters: parameters
                ),
                new (
                    methodName: handleMethod.Name,
                    rpcAlias: queryName,
                    customSerializerTypeFullName: customSerializer,
                    returnTypeKind: returnKind,
                    returnTypeFullName: returnTypeFullName,
                    parameters: parameters
                )
            };

            return new ServiceModel(
                classFullName: classFullName,
                serviceName: $"query_{queryName}",
                customSerializerTypeFullName: customSerializer,
                explicitExchangeName: exchangeName,
                explicitExchanges: exchanges,
                methods: methods,
                isQueryHandler: true,
                queryName: queryName
            );
        }

        return null;
    }

    private static string GetServiceName(INamedTypeSymbol symbol, AttributeData attr)
    {
        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string alias && !string.IsNullOrWhiteSpace(alias))
        {
            return alias.Trim().ToLowerInvariant();
        }

        var name = symbol.Name.ToLowerInvariant();
        if (name.EndsWith("service", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 7);
        }
        return string.IsNullOrEmpty(name) ? symbol.Name.ToLowerInvariant() : name;
    }

    private static string GetQueryName(INamedTypeSymbol symbol, AttributeData attr)
    {
        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string alias && !string.IsNullOrWhiteSpace(alias))
        {
            return alias.Trim().ToLowerInvariant();
        }

        var name = symbol.Name.ToLowerInvariant();
        var suffixes = new[] { "queryhandler", "commandhandler", "query", "command", "handler" };
        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - suffix.Length);
                break;
            }
        }
        return string.IsNullOrEmpty(name) ? symbol.Name.ToLowerInvariant() : name;
    }

    private static string GetMethodAlias(IMethodSymbol method, AttributeData attr)
    {
        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string alias && !string.IsNullOrWhiteSpace(alias))
        {
            return alias.Trim().ToLowerInvariant();
        }
        return method.Name.ToLowerInvariant();
    }

    private static string? GetSerializerTypeFromAttr(AttributeData attr)
    {
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg is { Key: "SerializerType", Value.Value: ITypeSymbol typeSymbol })
            {
                return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
        }
        return null;
    }

    private static (string? SerializerType, string? ExchangeName, List<string> Exchanges) ParseServiceAttributeProperties(AttributeData attr)
    {
        string? serializer = null;
        string? exchangeName = null;
        var exchanges = new List<string>();

        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg is { Key: "SerializerType", Value.Value: ITypeSymbol typeSymbol })
            {
                serializer = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }
            else if (namedArg is { Key: "ExchangeName", Value.Value: string exName })
            {
                exchangeName = exName;
            }
            else if (namedArg is { Key: "Exchanges", Value.Values: { } values })
            {
                foreach (var val in values)
                {
                    if (val.Value is string str && !string.IsNullOrWhiteSpace(str))
                    {
                        exchanges.Add(str);
                    }
                }
            }
        }

        return (serializer, exchangeName, exchanges);
    }

    private static (MethodReturnTypeKind Kind, string TypeFullName) AnalyzeReturnType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_Void)
        {
            return (MethodReturnTypeKind.Void, "void");
        }

        var fullDisplay = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (fullDisplay == "global::System.Threading.Tasks.Task")
        {
            return (MethodReturnTypeKind.TaskVoid, "void");
        }

        if (fullDisplay == "global::System.Threading.Tasks.ValueTask")
        {
            return (MethodReturnTypeKind.ValueTaskVoid, "void");
        }

        if (type is INamedTypeSymbol named)
        {
            if (named.IsGenericType && named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.Tasks.Task<TResult>")
            {
                var typeArg = named.TypeArguments[0];
                return (MethodReturnTypeKind.TaskOfT, typeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }

            if (named.IsGenericType && named.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Threading.Tasks.ValueTask<TResult>")
            {
                var typeArg = named.TypeArguments[0];
                return (MethodReturnTypeKind.ValueTypeOfT, typeArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
        }

        return (MethodReturnTypeKind.SyncOfT, fullDisplay);
    }

    private static List<ParameterModel> AnalyzeParameters(ImmutableArray<IParameterSymbol> parameters)
    {
        var result = new List<ParameterModel>();

        foreach (var p in parameters)
        {
            var typeDisplay = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var isCt = typeDisplay == "global::System.Threading.CancellationToken";
            var isNullable = p.NullableAnnotation == NullableAnnotation.Annotated ||
                             (!p.Type.IsValueType && p.NullableAnnotation != NullableAnnotation.NotAnnotated);

            var isComplex = IsComplexType(p.Type);

            string? defaultLiteral = null;
            if (p.HasExplicitDefaultValue)
            {
                defaultLiteral = p.ExplicitDefaultValue is null ? "null" : SymbolDisplay.FormatPrimitive(p.ExplicitDefaultValue, true, false);
            }

            result.Add(new ParameterModel(
                name: p.Name,
                typeFullName: typeDisplay,
                isCancellationToken: isCt,
                hasDefaultValue: p.HasExplicitDefaultValue,
                defaultValueLiteral: defaultLiteral,
                isNullable: isNullable,
                isComplexType: isComplex
            ));
        }

        return result;
    }

    private static bool IsComplexType(ITypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_Boolean or
            SpecialType.System_Byte or
            SpecialType.System_SByte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_Decimal or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_String or
            SpecialType.System_DateTime or
            SpecialType.System_Char)
        {
            return false;
        }

        var display = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (display is "global::System.Guid" or
                       "global::System.TimeSpan" or
                       "global::System.DateTimeOffset" or
                       "global::System.Threading.CancellationToken" or
                       "global::System.Text.Json.JsonElement")
        {
            return false;
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            return false;
        }

        if (type is INamedTypeSymbol { ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } named)
        {
            return IsComplexType(named.TypeArguments[0]);
        }

        return true;
    }

    private static void GenerateSources(SourceProductionContext spc, List<ServiceModel> services)
    {
        GenerateInvokersAndRegistry(spc, services);
    }

    private static string GenerateParameterExtraction(ParameterModel p, string elemVarName)
    {
        var type = p.TypeFullName;

        if (type is "int" or "global::System.Int32")
            return $"{elemVarName}.GetInt32()";
        if (type is "int?" or "global::System.Nullable<int>" or "global::System.Nullable<global::System.Int32>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (int?)null : {elemVarName}.GetInt32()";

        if (type is "long" or "global::System.Int64")
            return $"{elemVarName}.GetInt64()";
        if (type is "long?" or "global::System.Nullable<long>" or "global::System.Nullable<global::System.Int64>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (long?)null : {elemVarName}.GetInt64()";

        if (type is "short" or "global::System.Int16")
            return $"{elemVarName}.GetInt16()";
        if (type is "short?" or "global::System.Nullable<short>" or "global::System.Nullable<global::System.Int16>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (short?)null : {elemVarName}.GetInt16()";

        if (type is "byte" or "global::System.Byte")
            return $"{elemVarName}.GetByte()";
        if (type is "byte?" or "global::System.Nullable<byte>" or "global::System.Nullable<global::System.Byte>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (byte?)null : {elemVarName}.GetByte()";

        if (type is "bool" or "global::System.Boolean")
            return $"{elemVarName}.GetBoolean()";
        if (type is "bool?" or "global::System.Nullable<bool>" or "global::System.Nullable<global::System.Boolean>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (bool?)null : {elemVarName}.GetBoolean()";

        if (type is "double" or "global::System.Double")
            return $"{elemVarName}.GetDouble()";
        if (type is "double?" or "global::System.Nullable<double>" or "global::System.Nullable<global::System.Double>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (double?)null : {elemVarName}.GetDouble()";

        if (type is "float" or "global::System.Single")
            return $"{elemVarName}.GetSingle()";
        if (type is "float?" or "global::System.Nullable<float>" or "global::System.Nullable<global::System.Single>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (float?)null : {elemVarName}.GetSingle()";

        if (type is "decimal" or "global::System.Decimal")
            return $"{elemVarName}.GetDecimal()";
        if (type is "decimal?" or "global::System.Nullable<decimal>" or "global::System.Nullable<global::System.Decimal>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (decimal?)null : {elemVarName}.GetDecimal()";

        if (type is "string" or "global::System.String")
            return $"{elemVarName}.GetString()!";

        if (type is "global::System.Guid")
            return $"{elemVarName}.GetGuid()";
        if (type is "global::System.Guid?" or "global::System.Nullable<global::System.Guid>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (global::System.Guid?)null : {elemVarName}.GetGuid()";

        if (type is "global::System.DateTime")
            return $"{elemVarName}.GetDateTime()";
        if (type is "global::System.DateTime?" or "global::System.Nullable<global::System.DateTime>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (global::System.DateTime?)null : {elemVarName}.GetDateTime()";

        if (type is "global::System.DateTimeOffset")
            return $"{elemVarName}.GetDateTimeOffset()";
        if (type is "global::System.DateTimeOffset?" or "global::System.Nullable<global::System.DateTimeOffset>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (global::System.DateTimeOffset?)null : {elemVarName}.GetDateTimeOffset()";

        if (type is "global::System.TimeSpan")
            return $"global::System.TimeSpan.Parse({elemVarName}.GetString()!)";
        if (type is "global::System.TimeSpan?" or "global::System.Nullable<global::System.TimeSpan>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (global::System.TimeSpan?)null : global::System.TimeSpan.Parse({elemVarName}.GetString()!)";

        if (type is "global::System.DateOnly")
            return $"global::System.DateOnly.Parse({elemVarName}.GetString()!)";
        if (type is "global::System.DateOnly?" or "global::System.Nullable<global::System.DateOnly>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (global::System.DateOnly?)null : global::System.DateOnly.Parse({elemVarName}.GetString()!)";

        if (type is "global::System.TimeOnly")
            return $"global::System.TimeOnly.Parse({elemVarName}.GetString()!)";
        if (type is "global::System.TimeOnly?" or "global::System.Nullable<global::System.TimeOnly>")
            return $"{elemVarName}.ValueKind == JsonValueKind.Null ? (global::System.TimeOnly?)null : global::System.TimeOnly.Parse({elemVarName}.GetString()!)";

        if (type == "global::System.Text.Json.JsonElement")
            return $"{elemVarName}.Clone()";

        return $"DeserializeComplex<{type}>({elemVarName})";
    }

    private static void GenerateInvokersAndRegistry(SourceProductionContext spc, List<ServiceModel> services)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        sb.AppendLine("using Aid.Microservice.Server.Contracts;");
        sb.AppendLine("using Aid.Microservice.Server.Infrastructure;");
        sb.AppendLine("using Aid.Microservice.Server.Extensions;");
        sb.AppendLine("using Aid.Microservice.Shared.Models;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine();
        sb.AppendLine("namespace Aid.Microservice.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Source-generated, NativeAOT safe RPC method invokers and DI registry.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GeneratedRpcEndpoints");
        sb.AppendLine("{");
        sb.AppendLine("    private static readonly JsonSerializerOptions s_jsonOptions = new()");
        sb.AppendLine("    {");
        sb.AppendLine("        PropertyNameCaseInsensitive = true,");
        sb.AppendLine("        PropertyNamingPolicy = null");
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"Trimming\", \"IL2026:RequiresUnreferencedCode\", Justification = \"AOT parameter deserialization\")]");
        sb.AppendLine("    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"AOT\", \"IL3050:RequiresDynamicCode\", Justification = \"AOT parameter deserialization\")]");
        sb.AppendLine("    private static T DeserializeComplex<T>(JsonElement element)");
        sb.AppendLine("    {");
        sb.AppendLine("        var rawText = element.GetRawText();");
        sb.AppendLine("        return JsonSerializer.Deserialize<T>(rawText, s_jsonOptions)!;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"Trimming\", \"IL2026:RequiresUnreferencedCode\", Justification = \"AOT parameter deserialization\")]");
        sb.AppendLine("    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(\"AOT\", \"IL3050:RequiresDynamicCode\", Justification = \"AOT parameter deserialization\")]");
        sb.AppendLine("    private static T DeserializeFromDictionary<T>(global::System.Collections.Generic.Dictionary<string, JsonElement> parameters)");
        sb.AppendLine("    {");
        sb.AppendLine("        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(parameters, global::Aid.Microservice.Shared.Serialization.RpcSharedJsonContext.Default.DictionaryStringJsonElement);");
        sb.AppendLine("        return JsonSerializer.Deserialize<T>(jsonBytes, s_jsonOptions)!;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Generate Invoker Methods
        var invokerId = 0;
        var methodInvokerNames = new Dictionary<(string Service, string Method), string>();

        foreach (var svc in services)
        {
            var distinctMethods = svc.Methods.GroupBy(m => m.MethodName).Select(g => g.First()).ToList();

            foreach (var m in distinctMethods)
            {
                invokerId++;
                var invokerMethodName = $"Invoke_{Sanitize(svc.ServiceName)}_{Sanitize(m.MethodName)}_{invokerId}";
                methodInvokerNames[(svc.ServiceName, m.MethodName)] = invokerMethodName;

                sb.AppendLine($"    public static async global::System.Threading.Tasks.ValueTask<RpcResponse> {invokerMethodName}(");
                sb.AppendLine("        global::System.IServiceProvider serviceProvider,");
                sb.AppendLine("        global::System.Collections.Generic.Dictionary<string, JsonElement>? parameters,");
                sb.AppendLine("        global::System.Threading.CancellationToken cancellationToken)");
                sb.AppendLine("    {");
                sb.AppendLine("        using var scope = serviceProvider.CreateScope();");
                sb.AppendLine($"        var service = scope.ServiceProvider.GetRequiredService<{svc.ClassFullName}>();");
                sb.AppendLine();

                var callArgs = new List<string>();
                var nonCtParams = m.Parameters.Where(p => !p.IsCancellationToken).ToList();

                // If single complex parameter (e.g. CQRS request model)
                if (nonCtParams.Count == 1 && nonCtParams[0].IsComplexType)
                {
                    var targetParam = nonCtParams[0];
                    var varName = $"arg_{Sanitize(targetParam.Name)}";
                    sb.AppendLine($"        {targetParam.TypeFullName} {varName} = default!;");
                    sb.AppendLine("        if (parameters != null && parameters.Count > 0)");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            if (parameters.TryGetValue(\"{targetParam.Name}\", out var directElem) && directElem.ValueKind != JsonValueKind.Null)");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                {varName} = {GenerateParameterExtraction(targetParam, "directElem")};");
                    sb.AppendLine("            }");
                    sb.AppendLine("            else");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                {varName} = DeserializeFromDictionary<{targetParam.TypeFullName}>(parameters);");
                    sb.AppendLine("            }");
                    sb.AppendLine("        }");
                    sb.AppendLine();

                    callArgs.AddRange(m.Parameters.Select(p => p.IsCancellationToken ? "cancellationToken" : varName));
                }
                else
                {
                    // Multiple named parameters
                    for (var i = 0; i < m.Parameters.Count; i++)
                    {
                        var p = m.Parameters[i];
                        if (p.IsCancellationToken)
                        {
                            callArgs.Add("cancellationToken");
                            continue;
                        }

                        var varName = $"arg_{Sanitize(p.Name)}_{i}";
                        callArgs.Add(varName);

                        sb.AppendLine($"        {p.TypeFullName} {varName} = default!;");
                        sb.AppendLine($"        if (parameters != null && parameters.TryGetValue(\"{p.Name}\", out var elem_{i}))");
                        sb.AppendLine("        {");
                        sb.AppendLine($"            if (elem_{i}.ValueKind != JsonValueKind.Null)");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                {varName} = {GenerateParameterExtraction(p, $"elem_{i}")};");
                        sb.AppendLine("            }");
                        sb.AppendLine("        }");
                        sb.AppendLine("        else");
                        sb.AppendLine("        {");
                        if (p.HasDefaultValue)
                        {
                            sb.AppendLine($"            {varName} = {p.DefaultValueLiteral ?? "default"};");
                        }
                        else if (p.IsNullable)
                        {
                            sb.AppendLine($"            {varName} = default!;");
                        }
                        else
                        {
                            sb.AppendLine($"            throw new global::System.ArgumentException(\"Missing required parameter '{p.Name}'\");");
                        }
                        sb.AppendLine("        }");
                        sb.AppendLine();
                    }
                }

                var callExpr = $"service.{m.MethodName}({string.Join(", ", callArgs)})";

                if (m.ReturnTypeKind == MethodReturnTypeKind.Void)
                {
                    sb.AppendLine($"        {callExpr};");
                    sb.AppendLine("        return new RpcResponse { Result = null };");
                }
                else if (m.ReturnTypeKind is MethodReturnTypeKind.TaskVoid or MethodReturnTypeKind.ValueTaskVoid)
                {
                    sb.AppendLine($"        await {callExpr};");
                    sb.AppendLine("        return new RpcResponse { Result = null };");
                }
                else if (m.ReturnTypeKind is MethodReturnTypeKind.TaskOfT or MethodReturnTypeKind.ValueTypeOfT)
                {
                    sb.AppendLine($"        var result = await {callExpr};");
                    sb.AppendLine("        return new RpcResponse { Result = result };");
                }
                else
                {
                    sb.AppendLine($"        var result = {callExpr};");
                    sb.AppendLine("        return new RpcResponse { Result = result };");
                }

                sb.AppendLine("    }");
                sb.AppendLine();
            }
        }

        // Generate Extension Method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers source-generated RPC microservices, endpoints, and invokers into the DI container.");
        sb.AppendLine("    /// 100% NativeAOT and Trim safe.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddAidMicroserviceGenerated(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        services.AddAidMicroserviceCore();");
        sb.AppendLine();

        // Register each service type in DI
        foreach (var svc in services.Select(s => s.ClassFullName).Distinct())
        {
            sb.AppendLine($"        services.TryAddScoped<{svc}>();");
        }

        sb.AppendLine();
        sb.AppendLine("        services.Replace(ServiceDescriptor.Singleton<IRpcEndpointRegistry>(sp =>");
        sb.AppendLine("        {");
        sb.AppendLine("            var logger = sp.GetRequiredService<global::Microsoft.Extensions.Logging.ILogger<RpcEndpointRegistry>>();");
        sb.AppendLine("            var serializerRegistry = sp.GetRequiredService<ISerializerRegistry>();");
        sb.AppendLine("            var protocol = sp.GetRequiredService<global::Aid.Microservice.Shared.Interfaces.IRpcProtocol>();");
        sb.AppendLine("            var registry = new RpcEndpointRegistry(logger, serializerRegistry, protocol);");
        sb.AppendLine();

        // Register each method
        foreach (var svc in services)
        {
            var exchangeExpr = svc.ExplicitExchangeName != null ? $"\"{svc.ExplicitExchangeName}\"" : "null";

            foreach (var m in svc.Methods)
            {
                var invokerMethodName = methodInvokerNames[(svc.ServiceName, m.MethodName)];
                var serializerTypeExpr = m.CustomSerializerTypeFullName != null ? $"typeof({m.CustomSerializerTypeFullName})" : "null";

                sb.AppendLine($"            registry.RegisterEndpoint(");
                sb.AppendLine($"                serviceName: \"{svc.ServiceName}\",");
                sb.AppendLine($"                methodName: \"{m.RpcAlias}\",");
                sb.AppendLine($"                exchangeName: {exchangeExpr},");
                sb.AppendLine($"                serializerType: {serializerTypeExpr},");
                sb.AppendLine($"                invoker: {invokerMethodName});");
            }
        }

        sb.AppendLine();
        sb.AppendLine("            return registry;");
        sb.AppendLine("        }));");
        sb.AppendLine();
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        spc.AddSource("GeneratedRpcEndpoints.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static string Sanitize(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('_');
            }
        }
        return sb.ToString();
    }
}
