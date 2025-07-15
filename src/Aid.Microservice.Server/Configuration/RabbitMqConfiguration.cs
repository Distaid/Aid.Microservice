namespace Aid.Microservice.Server.Configuration;

public class RabbitMqConfiguration
{
    public string Hostname { get; set; } = null!;
    public int Port { get; set; }
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public int RetryCount { get; set; } = 3;
    public int RecoveryInterval { get; set; } = 5;
}