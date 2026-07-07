using System.ComponentModel.DataAnnotations;

namespace Aid.Microservice.Shared.Configuration;

public class RabbitMqConfiguration
{
    [Required]
    public string Hostname { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required]
    public string Username { get; set; } = "guest";

    [Required]
    public string Password { get; set; } = "guest";

    public string? ExchangeName { get; set; }

    [Range(0, int.MaxValue)]
    public int RetryCount { get; set; } = 3;

    [Range(1, int.MaxValue)]
    public int RecoveryInterval { get; set; } = 5;

    public bool DeleteExchangesOnShutdown { get; set; }

    public bool DeleteQueuesOnShutdown { get; set; }
}