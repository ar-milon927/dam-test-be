using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NinjaDAM.Services.IServices;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.DTO.ForgotPassword;
using NinjaDAM.DTO.User;
using Org.BouncyCastle.Crypto.Engines;
using System;
using System.Threading.Tasks;

namespace NinjaDAM.Services.Services
{  
    public class ForgotPasswordService : IForgotPasswordService
    {
        private readonly IRepository<Users> _userRepository;
        private readonly IEmailService _emailService;
        private readonly PasswordHasher<Users> _passwordHasher;
        private readonly IRepository<UserPasswordHistory> _passwordHistoryRepo;
        private readonly IMapper _mapper;

        public ForgotPasswordService( IRepository<Users> userRepository, IEmailService emailService, IMapper mapper, IRepository<UserPasswordHistory> passwordHistoryRepo)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _passwordHasher = new PasswordHasher<Users>();
            _passwordHistoryRepo = passwordHistoryRepo;
            _mapper = mapper;
        }

        public async Task<ForgotPasswordResponseDto> SendForgotPasswordOtpAsync(ForgotPasswordDto request)
        {
           // Get user from DB
            var user = await _userRepository.GetSingleAsync(u => u.Email.ToLower() == request.Email.ToLower());
            if (user == null)
            {
                return new ForgotPasswordResponseDto
                {
                    Success = false,
                    Message = "User not found with this email.",
                   
                };
            }
          
            // Send OTP using EmailService 
            await _emailService.SendOtpAsync(request.Email);

            return new ForgotPasswordResponseDto
            {
                Success = true,
                Message = "OTP is sent successfully to your email address.",
                FirstName = user.FirstName,
                LastName = user.LastName
            };
        }


        // Verify OTP and reset password  
        public async Task<VerifyOtpResponseDto> VerifyOtpAndResetPasswordAsync(string email, string otp, string newPassword)
        {
            var response = new VerifyOtpResponseDto();

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(otp) ||
                string.IsNullOrWhiteSpace(newPassword))
            {
                response.Success = false;
                response.Message = "Email, OTP, and new password are required.";
                return response;
            }

            // Verify OTP using EmailService  
            var (isVerified, verifiedEmail) = await _emailService.VerifyOtpAsync(email, otp);

            if (!isVerified)
            {
                response.Success = false;
                response.Message = "Invalid or expired OTP.";
                return response;
            }

            // Fetch user   
            var user = await _userRepository.GetSingleAsync(u => u.Email.ToLower() == verifiedEmail.ToLower());
            if (user == null)
            {
                response.Success = false;
                response.Message = "User not found.";
                return response;
            }

            // Optimized: fetch only last 5 passwords directly from DB
            var last5 = await _passwordHistoryRepo.Query()
                .Where(p => p.UserId == user.Id)
                .OrderByDescending(p => p.ChangedOn)
                .Take(5)
                .ToListAsync();

            foreach (var past in last5)
            {
                if (_passwordHasher.VerifyHashedPassword(user, past.PasswordHash, newPassword)
                    == PasswordVerificationResult.Success)
                {
                    response.Success = false;
                    response.Message = "You cannot reuse one of your last 5 passwords.";
                    return response;
                }
            }

            // Hash and update password  
            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            _userRepository.Update(user);
            await _userRepository.SaveAsync();

            //  Add new password to history 
            var newHistory = new UserPasswordHistory
            {
                UserId = user.Id,
                PasswordHash = user.PasswordHash,
                ChangedOn = DateTime.UtcNow
            };

            await _passwordHistoryRepo.AddAsync(newHistory);
            await _passwordHistoryRepo.SaveAsync();

            //  Optimized: delete older records (keep only latest 5)
            var oldToDelete = await _passwordHistoryRepo.Query()
                .Where(p => p.UserId == user.Id)
                .OrderByDescending(p => p.ChangedOn)
                .Skip(5)
                .ToListAsync();

            if (oldToDelete.Any())
            {
                foreach (var old in oldToDelete)
                    _passwordHistoryRepo.Delete(old);

                await _passwordHistoryRepo.SaveAsync();
            }

            response.Success = true;
            response.Message = "Password has been reset successfully.";
            response.Email = verifiedEmail;

            return response;
        }


    }
}
