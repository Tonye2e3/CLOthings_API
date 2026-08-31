using OtpNet;

namespace CLOthings_API.Services.Users
{
    public class TotpService
    {
        // 產生新的 TOTP Secret
        public string GenerateSecret()
        {
            var secretBytes = KeyGeneration.GenerateRandomKey(20);

            return Base32Encoding.ToString(secretBytes);
        }

        // 驗證使用者輸入的 6 位數驗證碼
        public bool VerifyCode(string secret, string code)
        {
            var secretBytes = Base32Encoding.ToBytes(secret);

            var totp = new Totp(secretBytes);

            return totp.VerifyTotp(
                code,
                out _,
                VerificationWindow.RfcSpecifiedNetworkDelay
            );
        }
    }
}