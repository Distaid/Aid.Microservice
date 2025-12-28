using Aid.Microservice.Client;

await using var factory = new RpcClientFactory("localhost", 5672, "guest", "guest");

await using var simpleClient = factory.CreateClient("simple");
var simpleResult = await simpleClient.CallAsync<int>("multiple", new {a = 5, b = 10});
Console.WriteLine(simpleResult);

await using var namedClient = factory.CreateClient("just_name_me");
var namedResult = await namedClient.CallAsync<int>("and_me", new {a = 5, b = 10});
Console.WriteLine(namedResult);

await using var asyncClient = factory.CreateClient("async");
await asyncClient.CallAsync("delay", new {seconds = 5});

await using var proxyClient = factory.CreateClient("proxy");
var proxyResult = await proxyClient.CallAsync<string>("multiplystring");
Console.WriteLine(proxyResult);

await using var diClient = factory.CreateClient("di");
await diClient.CallAsync<string>("log");