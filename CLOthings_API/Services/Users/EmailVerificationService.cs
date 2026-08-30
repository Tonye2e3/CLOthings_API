namespace CLOthings_API.Services.Users
{
    public class EmailVerificationService
    {
        private readonly EmailService _emailService;

        public EmailVerificationService(
            EmailService emailService)
        {
            _emailService = emailService;
        }

        public async Task SendVerificationEmailAsync(
            string toEmail,
            string verificationLink)
        {
            var subject = "CLOthings Email 驗證";

            var body = $"""
                您好：

                感謝您註冊 CLOthings。

                請使用以下連結完成 Email 驗證：

                {verificationLink}

                此連結將於 30 分鐘後失效。

                如果這不是您的操作，請忽略此信件。

                CLOthings
                """;

            await _emailService.SendAsync(
                toEmail,
                subject,
                body
            );
        }
    }
}