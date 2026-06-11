using MailKit.Net.Smtp;
using MailKit.Security;

using MimeKit;

using OneCFreshInvoiceODataBot.Models;

namespace OneCFreshInvoiceODataBot.Services;

public sealed class EmailSender(MailSettings settings)
{
    public async Task SendAsync(string subject, string body, IEnumerable<string> attachmentPaths, CancellationToken ct)
    {
        _ValidateSettings();

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(settings.From));
        message.To.Add(MailboxAddress.Parse(settings.ResultEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { TextBody = body };
        foreach (var path in attachmentPaths.Where(File.Exists))
            await builder.Attachments.AddAsync(path, ct);
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        var secure = settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, secure, ct);
        await client.AuthenticateAsync(settings.Login, settings.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    private void _ValidateSettings()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(settings.SmtpHost)) missing.Add("Mail:SmtpHost");
        if (string.IsNullOrWhiteSpace(settings.Login)) missing.Add("Mail:Login");
        if (string.IsNullOrWhiteSpace(settings.Password)) missing.Add("Mail:Password");
        if (string.IsNullOrWhiteSpace(settings.From)) missing.Add("Mail:From");
        if (string.IsNullOrWhiteSpace(settings.ResultEmail)) missing.Add("Mail:ResultEmail");
        if (missing.Count > 0)
            throw new InvalidOperationException("Не заполнены настройки почты: " + string.Join(", ", missing));
    }
}
