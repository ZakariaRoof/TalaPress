using System.Net;
using System.Net.Mail;
using Microsoft.Data.SqlClient;
using TalaPress.Infrastructure;

namespace TalaPress.Services;

public class SmtpEmailService : ISmtpEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ISecretProtector _secretProtector;

    public SmtpEmailService(IConfiguration configuration, ISecretProtector secretProtector)
    {
        _configuration = configuration;
        _secretProtector = secretProtector;
    }

    public async Task<(bool Success, string? Error)> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync();
        if (!settings.IsConfigured)
        {
            return (false, "إعدادات SMTP غير مفعّلة أو غير مكتملة.");
        }

        if (string.IsNullOrWhiteSpace(toEmail) || !toEmail.Contains('@'))
        {
            return (false, "عنوان البريد المستلم غير صالح.");
        }

        try
        {
            using var client = CreateClient(settings);
            using var message = new MailMessage
            {
                From = new MailAddress(settings.FromEmail!, settings.FromName ?? settings.FromEmail),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail.Trim());

            if (!string.IsNullOrWhiteSpace(plainTextBody))
            {
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                    plainTextBody, null, "text/plain"));
            }

            await client.SendMailAsync(message, cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public Task<(bool Success, string? Error)> SendTestAsync(string toEmail, CancellationToken cancellationToken = default)
    {
        const string subject = "TalaPress — اختبار SMTP";
        string html = """
            <p>تم إرسال هذه الرسالة من لوحة TalaPress للتحقق من إعدادات SMTP.</p>
            <p><small>If you received this, SMTP is configured correctly.</small></p>
            """;
        return SendAsync(toEmail, subject, html, "SMTP test from TalaPress.", cancellationToken);
    }

    private async Task<SmtpSettings> LoadSettingsAsync()
    {
        var settings = new SmtpSettings();
        string? connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return settings;
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string query = @"
                SELECT SmtpEnabled, SmtpHost, SmtpPort, SmtpUseSsl, SmtpUsername,
                       SmtpPasswordProtected, SmtpFromEmail, SmtpFromName
                FROM dbo.Settings WHERE Id = 1";

            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return settings;
            }

            settings.Enabled = !reader.IsDBNull(0) && reader.GetBoolean(0);
            settings.Host = reader.IsDBNull(1) ? null : reader.GetString(1);
            settings.Port = reader.IsDBNull(2) ? 587 : reader.GetInt32(2);
            settings.UseSsl = reader.IsDBNull(3) || reader.GetBoolean(3);
            settings.Username = reader.IsDBNull(4) ? null : reader.GetString(4);
            if (!reader.IsDBNull(5))
            {
                settings.Password = _secretProtector.Unprotect(reader.GetString(5));
            }
            settings.FromEmail = reader.IsDBNull(6) ? null : reader.GetString(6);
            settings.FromName = reader.IsDBNull(7) ? null : reader.GetString(7);
        }
        catch (SqlException)
        {
            // Settings columns may not exist yet during first deploy.
        }

        return settings;
    }

    private static SmtpClient CreateClient(SmtpSettings settings)
    {
        var client = new SmtpClient(settings.Host!, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password ?? string.Empty);
        }

        return client;
    }
}
