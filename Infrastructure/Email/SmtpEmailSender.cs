using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace smart_pet_care_api.Infrastructure.Email
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task SendAsync(string toAddress, string subject, string textBody, string? htmlBody = null)
        {
            // Dev fallback: without SMTP configured the email lands in the
            // console so the flow stays testable locally. The text part, not the
            // HTML -- the console is not a mail client, and the code is what
            // anyone reading this log is after.
            if (!_options.IsConfigured)
            {
                _logger.LogWarning(
                    "Email:Host is not configured — email NOT sent.\nTo: {To}\nSubject: {Subject}\n{Body}",
                    toAddress, subject, textBody);
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
            message.To.Add(MailboxAddress.Parse(toAddress));
            message.Subject = subject;

            // BodyBuilder puts the text part first in multipart/alternative, which
            // is the order the spec asks for: a client picks the last part it can
            // render, so HTML wins wherever it is supported.
            var builder = new BodyBuilder { TextBody = textBody };
            if (!string.IsNullOrWhiteSpace(htmlBody))
                builder.HtmlBody = htmlBody;

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTlsWhenAvailable);
            await client.AuthenticateAsync(_options.Username, _options.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
