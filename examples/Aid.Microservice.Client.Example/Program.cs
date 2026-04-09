using Aid.Microservice.Client;
using Aid.Microservice.Shared.Protocols;

await using var factory = new RpcClientFactory("localhost", 5672, "guest", "guest");

// --- Default protocol (DefaultJsonProtocol on "aid_rpc" exchange) ---
await using var simpleClient = factory.CreateClient("simple");
var simpleResult = await simpleClient.CallAsync<int>("multiple", new {a = 5, b = 10});
Console.WriteLine($"simple - multiple: {simpleResult}");

await using var namedClient = factory.CreateClient("just_name_me");
var namedResult = await namedClient.CallAsync<int>("and_me", new {a = 5, b = 10});
Console.WriteLine($"just_name_me - and_me: {namedResult}");

// --- Nameko protocol (NamekoSerializer on "nameko-rpc" exchange) ---
await using var namekoClient = factory.CreateClient("nameko_service", new NamekoProtocol());
var namekoResult = await namekoClient.CallAsync<int>("add", new {a = 3, b = 7});
Console.WriteLine($"nameko_service - add: {namekoResult}");

// --- Mixed service: call different methods on different exchanges ---
await using var mixedNamekoClient = factory.CreateClient("mixed_service", new NamekoProtocol());
var mixedNamekoResult = await mixedNamekoClient.CallAsync<int>("nameko_add", new {a = 10, b = 20});
Console.WriteLine($"mixed_service - nameko_add: {mixedNamekoResult}");

await using var mixedDefaultClient = factory.CreateClient("mixed_service");
var mixedDefaultResult = await mixedDefaultClient.CallAsync<int>("default_add", new {a = 100, b = 200});
Console.WriteLine($"mixed_service - default_add: {mixedDefaultResult}");

// --- Async / DI / Proxy ---
await using var asyncClient = factory.CreateClient("async");
await asyncClient.CallAsync("delay", new {seconds = 1});
Console.WriteLine("async - delay");

await using var proxyClient = factory.CreateClient("proxy");
var proxyResult = await proxyClient.CallAsync<string>("multiplystring");
Console.WriteLine($"proxy - multiplystring: {proxyResult}");

await using var diClient = factory.CreateClient("di");
await diClient.CallAsync<string>("log");
Console.WriteLine("di - log");