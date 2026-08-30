using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CLOthings_API.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendAsync(string toEmail, string subject, string body)
        {
            var host = _config["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host 尚未設定");
            var port = _config.GetValue<int>("Smtp:Port");
            var fromEmail = _config["Smtp:Email"] ?? throw new InvalidOperationException("Smtp:Email 尚未設定");
            var appPassword = _config["Smtp:AppPassword"] ?? throw new InvalidOperationException("Smtp:AppPassword 尚未設定");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("CLOthings 客服", fromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(fromEmail, appPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}