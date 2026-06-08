using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using OneCFreshInvoiceODataBot.Models;

namespace OneCFreshInvoiceODataBot.Services;

public sealed class EmailSender
{
    private readonly MailSettings _settings;

    public EmailSender(MailSettings settings)
    {
        _settings = settings;
    }

    public async Task SendAsync(string subject, string body, IEnumerable<string> attachmentPaths, CancellationToken ct)
    {
        ValidateSettings();

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_settings.From));
        message.To.Add(MailboxAddress.Parse(_settings.ResultEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { TextBody = body };
        foreach (var path in attachmentPaths.Where(File.Exists))
            await builder.Attachments.AddAsync(path, ct);
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        var secure = _settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, secure, ct);
        await client.AuthenticateAsync(_settings.Login, _settings.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    private void ValidateSettings()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_settings.SmtpHost)) missing.Add("Mail:SmtpHost");
        if (string.IsNullOrWhiteSpace(_settings.Login)) missing.Add("Mail:Login");
        if (string.IsNullOrWhiteSpace(_settings.Password)) missing.Add("Mail:Password");
        if (string.IsNullOrWhiteSpace(_settings.From)) missing.Add("Mail:From");
        if (string.IsNullOrWhiteSpace(_settings.ResultEmail)) missing.Add("Mail:ResultEmail");
        if (missing.Count > 0)
            throw new InvalidOperationException("Не заполнены настройки почты: " + string.Join(", ", missing));
    }
}
