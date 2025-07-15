namespace Aid.Microservice.Client.Configuration;

public class RabbitMqConfiguration
{
    public string Hostname { get; set; } = null!;
    public int Port { get; set; }
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}