using System;
using System.Threading.Tasks;
using AutoAllegro.Services.Interfaces;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AutoAllegro.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        public EmailSender(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }
        public Task SendEmailAsync(string to, string subject, string content, string replyTo, string displayName)
        {
            return Task.Run(() =>
            {
                var message = new MimeMessage();
                if (displayName == string.Empty)
                    displayName = null;

                message.From.Add(new MailboxAddress(displayName ?? "AutoAllegro", _settings.Mail));
                message.To.Add(new MailboxAddress(to));
                if (!string.IsNullOrEmpty(replyTo))
                {
                    message.ReplyTo.Add(new MailboxAddress(displayName ?? "AutoAllegro", replyTo));
                }

                message.Subject = subject;

                message.Body = new TextPart("html")
                {
                    Text = content
                };

                using (var client = new SmtpClient())
                {
                    client.Connect(_settings.SmtpServer, _settings.Port, false);

                    // Note: since we don't have an OAuth2 token, disable
                    // the XOAUTH2 authentication mechanism.
                    client.AuthenticationMechanisms.Remove("XOAUTH2");

                    // Note: only needed if the SMTP server requires authentication
                    client.Authenticate(_settings.Mail, _settings.Password);

                    client.Send(message);
                    client.Disconnect(true);
                }
            });
        }
    }

    public class EmailSettings
    {
        public string Mail { get; set; }
        public string Password { get; set; }
        public int Port { get; set; }
        public string SmtpServer { get; set; }
    }
}
