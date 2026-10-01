using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DentalClinic.Services
{
    public record EmailAttachment(string FileName, string ContentType, byte[] Content);

    public record EmailMessage(string To, string ToName, string Subject, string HtmlBody, EmailAttachment? Attachment = null);

    public interface IEmailSender
    {
        Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }

    public class EmailOptions
    {
        public const string Section = "Email";

        /// <summary>SMTP host. Empty means "do not send, only log".</summary>
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 25;
        public bool UseSsl { get; set; }
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = "no-reply@clinic.example";
        public string FromName { get; set; } = "Аврора Дент";
    }

    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;

        public SmtpEmailSender(IOptions<EmailOptions> options) => _options = options.Value;

        /// <summary>
        /// Encryption is either required or absent, never "when available": a downgrade to plain text would
        /// expose credentials and password reset links. Port 465 is implicit TLS, other ports use STARTTLS.
        /// </summary>
        public static SecureSocketOptions ChooseSecurity(EmailOptions options) =>
            !options.UseSsl ? SecureSocketOptions.None
            : options.Port == 465 ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            mime.To.Add(new MailboxAddress(message.ToName, message.To));
            mime.Subject = message.Subject;

            var body = new BodyBuilder { HtmlBody = message.HtmlBody };
            if (message.Attachment is { } file)
                body.Attachments.Add(file.FileName, file.Content, ContentType.Parse(file.ContentType));
            mime.Body = body.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_options.Host, _options.Port, ChooseSecurity(_options), cancellationToken);
            if (!string.IsNullOrEmpty(_options.User))
                await client.AuthenticateAsync(_options.User, _options.Password, cancellationToken);
            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
    }

    /// <summary>Used when no SMTP server is configured, so the app still works out of the box.</summary>
    public sealed class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Email to {Recipient} not sent (SMTP is not configured): {Subject}", message.To, message.Subject);
            return Task.CompletedTask;
        }
    }
}
