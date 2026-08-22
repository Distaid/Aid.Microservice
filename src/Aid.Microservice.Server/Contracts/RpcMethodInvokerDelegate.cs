using System.Text.Json;
using Aid.Microservice.Shared.Models;

namespace Aid.Microservice.Server.Contracts;

/// <summary>
/// Strongly-typed delegate for invoking an RPC method without reflection or runtime Expression compilation.
/// Safe for NativeAOT and trimming.
/// </summary>
/// <param name="serviceProvider">The root or scoped service provider to resolve services.</param>
/// <param name="parameters">The raw parameter dictionary received from the message protocol.</param>
/// <param name="cancellationToken">The cancellation token for the request lifecycle.</param>
/// <returns>The computed <see cref="RpcResponse"/>.</returns>
public delegate ValueTask<RpcResponse> RpcMethodInvokerDelegate(
    IServiceProvider serviceProvider,
    Dictionary<string, JsonElement>? parameters,
    CancellationToken cancellationToken);
