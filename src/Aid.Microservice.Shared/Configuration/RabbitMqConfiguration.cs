namespace Aid.Microservice.Shared.Configuration;

public class RabbitMqConfiguration
{
    public string Hostname { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "aid_rpc";
    public int RetryCount { get; set; } = 3;
    public int RecoveryInterval { get; set; } = 5;
}