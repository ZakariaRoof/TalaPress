namespace TalaPress.Services;

public class SmtpSettings
{
    public bool Enabled { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }

    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(FromEmail);
}

public interface ISmtpEmailService
{
    Task<(bool Success, string? Error)> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> SendTestAsync(
        string toEmail,
        CancellationToken cancellationToken = default);
}
