using Aid.Microservice.Shared.Attributes;

namespace Aid.Microservice.Server.Example.Services;

public record GetProductRequest(int Id, string Category);

public record ProductDto(int Id, string Name, string Category);

[MicroserviceQuery]
public class GetProductQueryHandler
{
    public async Task<ProductDto> HandleAsync(GetProductRequest request, CancellationToken token)
    {
        await Task.Yield();
        return new ProductDto(request.Id, $"Product #{request.Id}", request.Category);
    }
}
