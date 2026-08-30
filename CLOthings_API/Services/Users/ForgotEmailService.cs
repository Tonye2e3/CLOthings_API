namespace CLOthings_API.Services.Users
{
    public class ForgotEmailService
    {
        private readonly EmailService _emailService;

        public ForgotEmailService(EmailService emailService)
        {
            _emailService = emailService;
        }

        public async Task SendPasswordResetEmailAsync(
            string toEmail,
            string resetLink)
        {
            var subject = "CLOthings 密碼重設";

            var body = $"""
                您好：

                我們收到您的 CLOthings 密碼重設要求。

                請使用以下連結重新設定密碼：

                {resetLink}

                此連結將於 15 分鐘後失效。

                如果您沒有要求重設密碼，請忽略此信件。

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