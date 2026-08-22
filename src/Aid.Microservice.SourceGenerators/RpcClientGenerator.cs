using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aid.Microservice.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Aid.Microservice.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public class RpcClientGenerator : IIncrementalGenerator
{
    private const string MicroserviceClientAttributeName = "Aid.Microservice.Shared.Attributes.MicroserviceClientAttribute";
    private const string RpcCallableAttributeName = "Aid.Microservice.Shared.Attributes.RpcCallableAttribute";
    private const string MicroserviceQueryAttributeName = "Aid.Microservice.Shared.Attributes.MicroserviceQueryAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interfaceDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateInterface(s),
                transform: static (ctx, _) => GetInterfaceDeclaration(ctx))
            .Where(static m => m is not null);

        var compilationAndInterfaces = context.CompilationProvider.Combine(interfaceDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndInterfaces, static (spc, source) =>
        {
            var (compilation, interfaces) = source;
            Execute(compilation, interfaces!, spc);
        });
    }

    private static bool IsCandidateInterface(SyntaxNode node)
    {
        return node is InterfaceDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private static InterfaceDeclarationSyntax? GetInterfaceDeclaration(GeneratorSyntaxContext ctx)
    {
        return ctx.Node as InterfaceDeclarationSyntax;
    }

    private static void Execute(
        Compilation compilation,
        System.Collections.Immutable.ImmutableArray<InterfaceDeclarationSyntax> interfaceSyntaxes,
        SourceProductionContext context)
    {
        // Only generate client proxies if this project references Aid.Microservice.Client
        var rpcClientSymbol = compilation.GetTypeByMetadataName("Aid.Microservice.Client.Infrastructure.IRpcClient");
        if (rpcClientSymbol == null)
        {
            return;
        }

        var clientAttrSymbol = compilation.GetTypeByMetadataName(MicroserviceClientAttributeName);
        var rpcCallableAttrSymbol = compilation.GetTypeByMetadataName(RpcCallableAttributeName);
        var queryAttrSymbol = compilation.GetTypeByMetadataName(MicroserviceQueryAttributeName);

        var clientModels = new List<RpcClientInterfaceModel>();

        if (!interfaceSyntaxes.IsDefaultOrEmpty)
        {

            foreach (var syntax in interfaceSyntaxes.Distinct())
            {
                var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
                if (semanticModel.GetDeclaredSymbol(syntax) is not INamedTypeSymbol typeSymbol)
                {
                    continue;
                }

                var clientAttr = typeSymbol.GetAttributes().FirstOrDefault(ad =>
                    SymbolEqualityComparer.Default.Equals(ad.AttributeClass, clientAttrSymbol));

                if (clientAttr == null)
                {
                    continue;
                }

                var serviceName = clientAttr.ConstructorArguments.Length > 0
                    ? clientAttr.ConstructorArguments[0].Value?.ToString() ?? typeSymbol.Name.ToLowerInvariant()
                    : typeSymbol.Name.ToLowerInvariant();

                string? explicitExchange = null;
                string? customSerializerFullName = null;

                foreach (var namedArg in clientAttr.NamedArguments)
                {
                    if (namedArg is { Key: "ExchangeName", Value.Value: not null })
                    {
                        explicitExchange = namedArg.Value.Value.ToString();
                    }
                    else if (namedArg is { Key: "SerializerType", Value.Value: INamedTypeSymbol serializerSym })
                    {
                        customSerializerFullName = serializerSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    }
                }

                var methods = new List<RpcClientMethodModel>();

                foreach (var member in typeSymbol.GetMembers().OfType<IMethodSymbol>())
                {
                    if (member.MethodKind != MethodKind.Ordinary)
                    {
                        continue;
                    }

                    var rpcAlias = member.Name.ToLowerInvariant();
                    var isQuery = false;

                    var rpcAttr = member.GetAttributes().FirstOrDefault(ad =>
                        SymbolEqualityComparer.Default.Equals(ad.AttributeClass, rpcCallableAttrSymbol));
                    if (rpcAttr is { ConstructorArguments.Length: > 0 } && rpcAttr.ConstructorArguments[0].Value != null)
                    {
                        rpcAlias = rpcAttr.ConstructorArguments[0].Value!.ToString();
                    }

                    var qAttr = member.GetAttributes().FirstOrDefault(ad =>
                        SymbolEqualityComparer.Default.Equals(ad.AttributeClass, queryAttrSymbol));
                    if (qAttr != null)
                    {
                        isQuery = true;
                        if (qAttr.ConstructorArguments.Length > 0 && qAttr.ConstructorArguments[0].Value != null)
                        {
                            rpcAlias = qAttr.ConstructorArguments[0].Value!.ToString();
                        }
                    }

                    var (isAsync, isValueTask, returnsVoid, resultTypeFullName) = AnalyzeReturnType(member.ReturnType);

                    var parameters = new List<RpcClientParameterModel>();
                    foreach (var p in member.Parameters)
                    {
                        var pTypeFull = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        var isCt = pTypeFull.Contains("System.Threading.CancellationToken");
                        var isTimeout = pTypeFull.Contains("System.TimeSpan");
                        var hasDefault = p.HasExplicitDefaultValue;
                        string? defaultLiteral = null;

                        if (hasDefault)
                        {
                            defaultLiteral = p.ExplicitDefaultValue == null ? "default" : p.ExplicitDefaultValue.ToString();
                        }

                        parameters.Add(new RpcClientParameterModel(
                            name: p.Name,
                            typeFullName: pTypeFull,
                            isCancellationToken: isCt,
                            isTimeout: isTimeout,
                            hasDefaultValue: hasDefault,
                            defaultValueLiteral: defaultLiteral));
                    }

                    methods.Add(new RpcClientMethodModel(
                        methodName: member.Name,
                        rpcAlias: rpcAlias,
                        isQuery: isQuery,
                        returnTypeFullName: member.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        isAsync: isAsync,
                        isValueTask: isValueTask,
                        returnsVoid: returnsVoid,
                        resultTypeFullName: resultTypeFullName,
                        parameters: parameters));
                }

                var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
                    ? "Aid.Microservice.Generated"
                    : typeSymbol.ContainingNamespace.ToDisplayString();

                clientModels.Add(new RpcClientInterfaceModel(
                    interfaceName: typeSymbol.Name,
                    fullInterfaceName: typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    @namespace: ns,
                    serviceName: serviceName,
                    explicitExchangeName: explicitExchange,
                    customSerializerTypeFullName: customSerializerFullName,
                    methods: methods));
            }
        }

        if (clientModels.Count > 0)
        {
            var code = GenerateClientsSource(clientModels);
            context.AddSource("GeneratedRpcClients.g.cs", SourceText.From(code, Encoding.UTF8));
        }
    }

    private static (bool IsAsync, bool IsValueTask, bool ReturnsVoid, string? ResultTypeFullName) AnalyzeReturnType(ITypeSymbol returnType)
    {
        var fullName = returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (fullName == "void")
        {
            return (false, false, true, null);
        }

        if (fullName.StartsWith("global::System.Threading.Tasks.Task<") && returnType is INamedTypeSymbol
            {
                TypeArguments.Length: 1
            } taskType)
        {
            var argType = taskType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return (true, false, false, argType);
        }

        if (fullName == "global::System.Threading.Tasks.Task")
        {
            return (true, false, true, null);
        }

        if (fullName.StartsWith("global::System.Threading.Tasks.ValueTask<") && returnType is INamedTypeSymbol
            {
                TypeArguments.Length: 1
            } vtType)
        {
            var argType = vtType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return (true, true, false, argType);
        }

        if (fullName == "global::System.Threading.Tasks.ValueTask")
        {
            return (true, true, true, null);
        }

        return (false, false, false, fullName);
    }

    private static string GenerateClientsSource(List<RpcClientInterfaceModel> clients)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        sb.AppendLine("using Aid.Microservice.Client.Infrastructure;");
        sb.AppendLine("using Aid.Microservice.Shared.Interfaces;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();
        sb.AppendLine("namespace Aid.Microservice.Generated;");
        sb.AppendLine();

        foreach (var client in clients)
        {
            var implClassName = $"{client.InterfaceName.TrimStart('I')}RpcClient";

            sb.AppendLine($"/// <summary>");
            sb.AppendLine($"/// Strongly-typed, NativeAOT safe RPC client implementation of <see cref=\"{client.FullInterfaceName}\"/>.");
            sb.AppendLine($"/// </summary>");
            sb.AppendLine($"internal sealed class {implClassName} : {client.FullInterfaceName}");
            sb.AppendLine("{");
            sb.AppendLine("    private readonly IRpcClient _innerClient;");
            sb.AppendLine();
            sb.AppendLine($"    public {implClassName}(IRpcClientFactory clientFactory)");
            sb.AppendLine("    {");

            sb.AppendLine(client.ExplicitExchangeName != null
                ? $"        _innerClient = clientFactory.CreateClient(\"{client.ServiceName}\", protocol: null!, exchangeName: \"{client.ExplicitExchangeName}\");"
                : $"        _innerClient = clientFactory.CreateClient(\"{client.ServiceName}\");");

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine($"    public {implClassName}(IRpcClient innerClient)");
            sb.AppendLine("    {");
            sb.AppendLine("        _innerClient = innerClient;");
            sb.AppendLine("    }");
            sb.AppendLine();

            foreach (var m in client.Methods)
            {
                var paramListStr = string.Join(", ", m.Parameters.Select(p =>
                {
                    var def = p.HasDefaultValue ? $" = {p.DefaultValueLiteral}" : "";
                    return $"{p.TypeFullName} {p.Name}{def}";
                }));

                sb.AppendLine($"    public async {m.ReturnTypeFullName} {m.MethodName}({paramListStr})");
                sb.AppendLine("    {");

                var ctParam = m.Parameters.FirstOrDefault(p => p.IsCancellationToken);
                var ctExpr = ctParam != null ? ctParam.Name : "default";

                var timeoutParam = m.Parameters.FirstOrDefault(p => p.IsTimeout);
                var timeoutExpr = timeoutParam != null ? timeoutParam.Name : "null";

                var dataParams = m.Parameters.Where(p => !p.IsCancellationToken && !p.IsTimeout).ToList();

                if (dataParams.Count == 0)
                {
                    sb.AppendLine("        object? payload = null;");
                }
                else if (dataParams.Count == 1 && (m.IsQuery || dataParams[0].Name.Equals("request", System.StringComparison.OrdinalIgnoreCase)))
                {
                    sb.AppendLine($"        var payload = {dataParams[0].Name};");
                }
                else
                {
                    var anonProps = string.Join(", ", dataParams.Select(p => $"{p.Name}"));
                    sb.AppendLine($"        var payload = new {{ {anonProps} }};");
                }

                var targetCall = m.IsQuery ? "CallQueryAsync" : "CallAsync";

                if (m.ReturnsVoid)
                {
                    sb.AppendLine($"        await _innerClient.{targetCall}(\"{m.RpcAlias}\", payload, {timeoutExpr}, {ctExpr}).ConfigureAwait(false);");
                }
                else
                {
                    sb.AppendLine($"        var result = await _innerClient.{targetCall}<{m.ResultTypeFullName}>(\"{m.RpcAlias}\", payload, {timeoutExpr}, {ctExpr}).ConfigureAwait(false);");
                    sb.AppendLine($"        return result!;");
                }

                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Generate Extension method
        sb.AppendLine("public static class GeneratedRpcClientsExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Registers all source-generated strongly-typed RPC clients into the service collection.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IServiceCollection AddAidMicroserviceGeneratedClients(this IServiceCollection services)");
        sb.AppendLine("    {");

        foreach (var client in clients)
        {
            var implClassName = $"{client.InterfaceName.TrimStart('I')}RpcClient";
            sb.AppendLine($"        services.TryAddTransient<{client.FullInterfaceName}, {implClassName}>();");
        }

        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
