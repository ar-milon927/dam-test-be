namespace NinjaDAM.Services.IServices
{


    
        public interface IEmailService
        {
         
            Task<string> SendOtpAsync(string email);

            Task<(bool IsVerified, string? Email)> VerifyOtpAsync(string email, string otp);
             Task SendEmailAsync(string toEmail, string subject, string body);
              Task SendTemplatedEmailAsync(string toEmail, string subject, string templateName, Dictionary<string, string> placeholders);
        }
}


