using Aid.Microservice.Shared.Attributes;

namespace Aid.Microservice.Client.Tests.TestContracts;

public record TaxRequest(decimal Amount, decimal Rate, string? Country);
public record TaxResult(decimal Total, decimal Tax);

[MicroserviceClient("calc")]
public interface ICalculatorRpcClient
{
    [RpcCallable("add")]
    Task<int> Add(int a, int b, CancellationToken cancellationToken = default);

    [RpcCallable("async_square")]
    Task<int> AsyncSquare(int value, CancellationToken cancellationToken = default);
}

[MicroserviceClient("query_calc_tax")]
public interface ITaxRpcClient
{
    [MicroserviceQuery("calc_tax")]
    Task<TaxResult> CalculateTax(TaxRequest request, CancellationToken cancellationToken = default);
}
