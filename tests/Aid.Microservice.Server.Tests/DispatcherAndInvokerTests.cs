using System.Text.Json;
using Aid.Microservice.Generated;
using Aid.Microservice.Server.Extensions;
using Aid.Microservice.Server.Infrastructure;
using Aid.Microservice.Server.Tests.TestServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aid.Microservice.Server.Tests;

public class DispatcherAndInvokerTests
{
    private readonly IRpcRequestDispatcher _dispatcher;
    private readonly IRpcEndpointRegistry _registry;

    public DispatcherAndInvokerTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAidMicroserviceGenerated();

        IServiceProvider serviceProvider = services.BuildServiceProvider();
        _dispatcher = serviceProvider.GetRequiredService<IRpcRequestDispatcher>();
        _registry = serviceProvider.GetRequiredService<IRpcEndpointRegistry>();
    }

    [Fact]
    public void EndpointRegistry_ShouldContainGeneratedEndpoints()
    {
        _registry.TryGetMethod("calc", "add", out var addInfo).Should().BeTrue();
        addInfo.Should().NotBeNull();
        addInfo.Invoker.Should().NotBeNull();

        _registry.TryGetMethod("calc", "divide", out var divInfo).Should().BeTrue();
        divInfo.Should().NotBeNull();

        _registry.TryGetMethod("query_calc_tax", "calc_tax", out var taxInfo).Should().BeTrue();
        taxInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task DispatchAsync_SyncMethod_ReturnsCorrectResult()
    {
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = JsonSerializer.SerializeToElement(15),
            ["b"] = JsonSerializer.SerializeToElement(27)
        };

        var response = await _dispatcher.DispatchAsync("calc", "add", parameters);

        response.IsSuccess.Should().BeTrue();
        response.Error.Should().BeNull();
        response.Result.Should().Be(42);
    }

    [Fact]
    public async Task DispatchAsync_AsyncMethod_ReturnsCorrectResult()
    {
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["value"] = JsonSerializer.SerializeToElement(9)
        };

        var response = await _dispatcher.DispatchAsync("calc", "async_square", parameters);

        response.IsSuccess.Should().BeTrue();
        response.Result.Should().Be(81);
    }

    [Fact]
    public async Task DispatchAsync_ValueTaskMethod_ReturnsCorrectResult()
    {
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["value"] = JsonSerializer.SerializeToElement(100L)
        };

        var response = await _dispatcher.DispatchAsync("calc", "value_task_triple", parameters);

        response.IsSuccess.Should().BeTrue();
        response.Result.Should().Be(300L);
    }

    [Fact]
    public async Task DispatchAsync_VoidAndAsyncVoidMethods_Succeed()
    {
        var voidResp = await _dispatcher.DispatchAsync("calc", "do_nothing", null);
        voidResp.IsSuccess.Should().BeTrue();
        voidResp.Result.Should().BeNull();

        var asyncVoidResp = await _dispatcher.DispatchAsync("calc", "async_void", null);
        asyncVoidResp.IsSuccess.Should().BeTrue();
        asyncVoidResp.Result.Should().BeNull();
    }

    [Fact]
    public async Task DispatchAsync_DefaultParameter_UsesDefaultWhenOmitted()
    {
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = JsonSerializer.SerializeToElement("Alice")
        };

        var response = await _dispatcher.DispatchAsync("calc", "greet", parameters);

        response.IsSuccess.Should().BeTrue();
        response.Result.Should().Be("Hello, Alice!");
    }

    [Fact]
    public async Task DispatchAsync_DefaultParameter_OverriddenWhenProvided()
    {
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = JsonSerializer.SerializeToElement("Bob"),
            ["prefix"] = JsonSerializer.SerializeToElement("Welcome")
        };

        var response = await _dispatcher.DispatchAsync("calc", "greet", parameters);

        response.IsSuccess.Should().BeTrue();
        response.Result.Should().Be("Welcome, Bob!");
    }

    [Fact]
    public async Task DispatchAsync_MissingRequiredParameter_ReturnsError()
    {
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = JsonSerializer.SerializeToElement(10)
            // "b" is missing
        };

        var response = await _dispatcher.DispatchAsync("calc", "add", parameters);

        response.IsSuccess.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.Message.Should().Contain("b");
    }

    [Fact]
    public async Task DispatchAsync_ExceptionInService_ReturnsErrorResponse()
    {
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = JsonSerializer.SerializeToElement(10.0),
            ["b"] = JsonSerializer.SerializeToElement(0.0)
        };

        var response = await _dispatcher.DispatchAsync("calc", "divide", parameters);

        response.IsSuccess.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error!.ErrorType.Should().Be(nameof(DivideByZeroException));
        response.Error.Message.Should().Be("Cannot divide by zero");
    }

    [Fact]
    public async Task DispatchAsync_MicroserviceQuery_ComplexRequestObject_BindsSuccessfully()
    {
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["amount"] = JsonSerializer.SerializeToElement(100.0m),
            ["rate"] = JsonSerializer.SerializeToElement(0.20m),
            ["country"] = JsonSerializer.SerializeToElement("US")
        };

        var response = await _dispatcher.DispatchAsync("query_calc_tax", "calc_tax", parameters);

        response.IsSuccess.Should().BeTrue();
        response.Result.Should().BeOfType<TaxResult>();
        var result = (TaxResult)response.Result!;
        result.Tax.Should().Be(20.0m);
        result.Total.Should().Be(120.0m);
    }

    [Fact]
    public async Task DispatchAsync_ModernTypes_DateOnlyTimeOnlyGuid_ParseCorrectly()
    {
        var id = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 22);
        var time = new TimeOnly(14, 30, 0);

        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["date"] = JsonSerializer.SerializeToElement(date.ToString("O")),
            ["time"] = JsonSerializer.SerializeToElement(time.ToString("O")),
            ["id"] = JsonSerializer.SerializeToElement(id)
        };

        var response = await _dispatcher.DispatchAsync("calc", "format_date", parameters);

        response.IsSuccess.Should().BeTrue();
        response.Result.Should().Be($"{id}: {date} {time}");
    }

    [Fact]
    public async Task DispatchAsync_NullableParameters_SupportNullAndDefaults()
    {
        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = JsonSerializer.SerializeToElement((int?)null)
            // factor is omitted -> defaults to 1.5
        };

        var response = await _dispatcher.DispatchAsync("calc", "calc_optional", parameters);

        response.IsSuccess.Should().BeTrue();
        response.Result.Should().Be(15.0); // (10) * 1.5
    }

    [Fact]
    public async Task Legacy_AddAidMicroservice_ReflectionBased_WorksCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddAidMicroservice(typeof(CalculatorService).Assembly);
#pragma warning restore CS0618

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IRpcRequestDispatcher>();

        var parameters = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = JsonSerializer.SerializeToElement(20),
            ["b"] = JsonSerializer.SerializeToElement(30)
        };

        var response = await dispatcher.DispatchAsync("calc", "add", parameters);

        response.IsSuccess.Should().BeTrue();
        response.Result.Should().Be(50);
    }
}
