using Aid.Microservice.Client;
using Aid.Microservice.Shared.Models;
using Aid.Microservice.Shared.Protocols;

await using var factory = new RpcClientFactory("80.209.241.39", 5672, "guest", "guest");

// --- Default protocol (DefaultJsonProtocol on "aid_rpc" exchange) ---
await using var simpleClient = factory.CreateClient("simple");
var simpleResult = await simpleClient.CallAsync<int>("multiple", new {a = 5, b = 10});
Console.WriteLine($"[default] simple.multiple(a=5, b=10) => {simpleResult}");

await using var namedClient = factory.CreateClient("just_name_me");
var namedResult = await namedClient.CallAsync<int>("and_me", new {a = 5, b = 10});
Console.WriteLine($"[default] just_name_me.and_me(a=5, b=10) => {namedResult}");

// --- Nameko protocol (NamekoSerializer on "nameko-rpc" exchange) ---
await using var namekoClient = factory.CreateClient("nameko_service", new NamekoProtocol());
var namekoResult = await namekoClient.CallAsync<int>("add", new {a = 3, b = 7});
Console.WriteLine($"[nameko] nameko_service.add(a=3, b=7) => {namekoResult}");

// --- Mixed service: call different methods on different exchanges ---
await using var mixedNamekoClient = factory.CreateClient("mixed_service", new NamekoProtocol());
var mixedNamekoResult = await mixedNamekoClient.CallAsync<int>("nameko_add", new {a = 10, b = 20});
Console.WriteLine($"[nameko] mixed_service.nameko_add(a=10, b=20) => {mixedNamekoResult}");

await using var mixedDefaultClient = factory.CreateClient("mixed_service");
var mixedDefaultResult = await mixedDefaultClient.CallAsync<int>("default_add", new {a = 100, b = 200});
Console.WriteLine($"[default] mixed_service.default_add(a=100, b=200) => {mixedDefaultResult}");

// --- Async / DI / Proxy ---
await using var asyncClient = factory.CreateClient("async");
await asyncClient.CallAsync("delay", new {seconds = 1});
Console.WriteLine("[default] async.delay(seconds=1) => done");

await using var proxyClient = factory.CreateClient("proxy");
var proxyResult = await proxyClient.CallAsync<string>("multiplystring");
Console.WriteLine($"[default] proxy.multiplystring() => {proxyResult}");

await using var diClient = factory.CreateClient("di");
await diClient.CallAsync<string>("log");
Console.WriteLine("[default] di.log() => done");

// =========================================================================
// NAMEKO (Python) RPC CALLS
// =========================================================================
// Nameko server:
//
//   from nameko.rpc import rpc
//
//   class GreetingService:
//       name = "greeting_service"
//
//       @rpc
//       def hello(self, name):
//           return f"Hello, {name}!"
// =========================================================================

// --- Method 1: Anonymous object (kwargs) ---
// Python equivalent: hello(name="World")
await using var namekoClient1 = factory.CreateClient("greeting_service", new NamekoProtocol());
var result1 = await namekoClient1.CallAsync<string>("hello", new { name = "World" });
Console.WriteLine($"[kwargs] greeting_service.hello(name='World') => {result1}");

// --- Method 2: RpcNamekoRequest with positional args ---
// Python equivalent: hello("JDog")
await using var namekoClient2 = factory.CreateClient("greeting_service", new NamekoProtocol());
var result2 = await namekoClient2.CallAsync<string>("hello", new RpcNamekoRequest("JDog"));
Console.WriteLine($"[args] greeting_service.hello('JDog') => {result2}");

// --- Method 3: RpcNamekoRequest with explicit kwargs ---
// Python equivalent: hello(name="Alice")
await using var namekoClient3 = factory.CreateClient("greeting_service", new NamekoProtocol());
var result3 = await namekoClient3.CallAsync<string>("hello", new RpcNamekoRequest([], new { name = "Alice" }));
Console.WriteLine($"[kwargs] greeting_service.hello(name='Alice') => {result3}");
