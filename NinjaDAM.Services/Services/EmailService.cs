using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;
using System.Net;
using System.Net.Mail;

namespace NinjaDAM.Services.Services
{
    /// <summary>
    /// Centralized email handling service.
    /// Responsible for sending emails (templated or plain), 
    /// managing OTPs, and verifying them for actions like password resets or email verification.
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly IRepository<VerifyEmail> _otpRepository;
        private readonly IRepository<Users> _userRepository;
        private readonly UserManager<Users> _userManager;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;

        public EmailService(
            IRepository<VerifyEmail> otpRepository,
            IRepository<Users> userRepository,
            UserManager<Users> userManager,
            IConfiguration configuration,
            IMapper mapper)
        {
            _otpRepository = otpRepository;
            _userRepository = userRepository;
            _userManager = userManager;
            _configuration = configuration;
            _mapper = mapper;
        }

        #region TEMPLATE HANDLING

        /// <summary>
        /// Loads an HTML email template from wwwroot/Templates/Email folder.
        /// </summary>
        private async Task<string> LoadEmailTemplateAsync(string templateName)
        {
            string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Templates", "Email");
            string templatePath = Path.Combine(basePath, $"{templateName}.html");

            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Email template '{templateName}.html' not found at {templatePath}");
          
            return await File.ReadAllTextAsync(templatePath);
        }

        /// <summary>
        /// Replaces placeholders in the HTML template with actual dynamic values.
        /// Example placeholders: {{FirstName}}, {{OTP}}, {{Email}}
        /// </summary>
        private string ReplacePlaceholders(string template, Dictionary<string, string> placeholders)
        {
            foreach (var placeholder in placeholders)
                template = template.Replace(placeholder.Key, placeholder.Value ?? string.Empty);

            return template;
        }

        #endregion
        
        #region GENERIC TEMPLATE EMAIL SENDER

        /// <summary>
        /// Sends an email using a given HTML template and a set of dynamic placeholders.
        /// This makes the service reusable for multiple email types (Forgot Password, Welcome Email, etc.) 
        /// </summary>
        public async Task SendTemplatedEmailAsync( string toEmail,string subject,string templateName, Dictionary<string, string> placeholders)
        {
            // Load the template content
            string htmlBody = await LoadEmailTemplateAsync(templateName);

            // Replace placeholders with actual values
            htmlBody = ReplacePlaceholders(htmlBody, placeholders);

            // Send the final email
            await SendEmailAsync(toEmail, subject, htmlBody);
        }

        #endregion

        #region OTP MANAGEMENT

        /// <summary>
        /// Generates a new OTP, saves it to the database, and sends it to the user's email using a template.
        /// Automatically removes any old OTP entries for the same email.
        /// </summary> 
        public async Task<string> SendOtpAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email address is required.", nameof(email));

            email = email.Trim().ToLowerInvariant();

            // Delete old OTP records for this email to avoid duplicates
            var existingOtps = (await _otpRepository.FindAsync(o => o.Email == email)).ToList();
            foreach (var otp in existingOtps)
                _otpRepository.Delete(otp);

            if (existingOtps.Any())
                await _otpRepository.SaveAsync();

            // Generate new OTP (6 digits)
            var otpCode = new Random().Next(100000, 999999).ToString();
            var expiryTime = DateTime.UtcNow.AddMinutes(5); // OTP expires in 5 minutes

            var newOtp = new VerifyEmail
            {
                Email = email,
                Otp = otpCode,
                ExpiryTime = expiryTime,
                IsVerified = false
            };

            // Save OTP in the database
            await _otpRepository.AddAsync(newOtp);
            await _otpRepository.SaveAsync();

            // Get user info (for personalization)
            var user = (await _userRepository.FindAsync(u => u.Email.ToLower() == email)).FirstOrDefault();

            // Define dynamic placeholders for the email template
            var placeholders = new Dictionary<string, string>
            {
                { "{{FirstName}}", user?.FirstName ?? "" },
                { "{{LastName}}", user?.LastName ?? "" },
                { "{{OTP}}", otpCode },
                { "{{Email}}", email }
            };

            // Send email using the ForgotPassword.html template
            await SendTemplatedEmailAsync(email, "Your One-Time Password (OTP)", "ForgotPassword", placeholders);

            return "An OTP has been sent to your email address.";
        }

        /// <summary>
        /// Verifies whether the provided OTP is valid, not expired, and not previously used.
        /// </summary>
        public async Task<(bool IsVerified, string? Email)> VerifyOtpAsync(string email, string otp)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
                return (false, null);

            email = email.Trim().ToLowerInvariant();
            otp = otp.Trim();

            // Find matching OTP record that is not expired or already verified
            var otpRecord = (await _otpRepository.FindAsync(o =>
                o.Email == email &&
                o.Otp == otp &&
                o.ExpiryTime >= DateTime.UtcNow &&
                !o.IsVerified)).FirstOrDefault();

            if (otpRecord == null)
                return (false, null);

            // Mark OTP as verified
            otpRecord.IsVerified = true;
            _otpRepository.Update(otpRecord);
            await _otpRepository.SaveAsync();

            return (true, email);
        } 

        #endregion

        #region CORE EMAIL SENDER (SMTP)

        /// <summary>
        /// Sends an email using SMTP (Gmail, Zoho, or any configured provider).
        /// Supports HTML body for templates.
        /// </summary>
        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var fromEmail = _configuration["Smtp:Email"];
                var fromPassword = _configuration["Smtp:Password"];
                var smtpHost = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
                var smtpPort = int.TryParse(_configuration["Smtp:Port"], out int port) ? port : 587;

                if (string.IsNullOrWhiteSpace(fromEmail) || string.IsNullOrWhiteSpace(fromPassword))
                    throw new InvalidOperationException("SMTP configuration is missing or incomplete.");

                using var smtpClient = new SmtpClient(smtpHost)
                {
                    Port = smtpPort,
                    Credentials = new NetworkCredential(fromEmail, fromPassword),
                    EnableSsl = true,
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true // Enables HTML formatting in email templates 
                };

                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to send email. Please try again later.", ex);
            }
        }

        #endregion
    }
}
