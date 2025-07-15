using Aid.Microservice.Client;

await using var client = new RpcClient("192.168.100.99", 5672, "distaid", "91375");
await client.InitializeAsync();

var simpleResult = await client.CallAsync<int>("simple", "multiple", new {a = 5, b = 10});
Console.WriteLine(simpleResult);

var namedResult = await client.CallAsync<int>("just_name_me", "and_me", new {a = 5, b = 10});
Console.WriteLine(namedResult);

await client.CallAsynс("async", "delay", new {seconds = 5});

var proxyResult = await client.CallAsync<string>("proxy", "multiplystring");
Console.WriteLine(proxyResult);

await client.CallAsync<string>("di", "log");