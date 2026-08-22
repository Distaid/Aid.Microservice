using Aid.Microservice.Shared.Attributes;
using Aid.Microservice.Shared.Protocols;

namespace Aid.Microservice.Server.Tests.TestServices;

[Microservice("calc")]
public class CalculatorService
{
    [RpcCallable("add")]
    public int Add(int a, int b) => a + b;

    [RpcCallable("divide")]
    public double Divide(double a, double b)
    {
        if (b == 0) throw new DivideByZeroException("Cannot divide by zero");
        return a / b;
    }

    [RpcCallable("greet")]
    public string Greet(string name, string prefix = "Hello") => $"{prefix}, {name}!";

    [RpcCallable("async_square")]
    public async Task<int> AsyncSquare(int value)
    {
        await Task.Yield();
        return value * value;
    }

    [RpcCallable("value_task_triple")]
    public async ValueTask<long> ValueTaskTriple(long value)
    {
        await Task.Yield();
        return value * 3;
    }

    [RpcCallable("format_date")]
    public string FormatDate(DateOnly date, TimeOnly? time, Guid id) => $"{id}: {date} {time?.ToString() ?? "no-time"}";

    [RpcCallable("calc_optional")]
    public double CalcOptional(int? a, double? factor = 1.5) => (a ?? 10) * (factor ?? 1.0);

    [RpcCallable("do_nothing")]
    public void DoNothing()
    {
    }

    [RpcCallable("async_void")]
    public async Task AsyncVoid(CancellationToken ct)
    {
        await Task.Delay(1, ct);
    }
}

public record TaxRequest(decimal Amount, decimal Rate, string? Country);
public record TaxResult(decimal Total, decimal Tax);

[MicroserviceQuery("calc_tax")]
public class CalculateTaxQueryHandler
{
    public async Task<TaxResult> HandleAsync(TaxRequest request, CancellationToken cancellationToken)
    {
        await Task.Yield();
        var tax = request.Amount * request.Rate;
        return new TaxResult(request.Amount + tax, tax);
    }
}

[Microservice("py_math", SerializerType = typeof(NamekoSerializer))]
public class TestNamekoService
{
    [RpcCallable("sum")]
    public int Sum(int x, int y) => x + y;
}
