using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NinjaDAM.Services.IServices;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.DTO.ResetPassword;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NinjaDAM.Services.Services
{
    public class ResetPasswordService : IResetPasswordService
    {
        private readonly UserManager<Users> _userManager;
        private readonly IRepository<Users> _userRepo;
        private readonly IRepository<Company> _companyRepo;
        private readonly IRepository<UserPasswordHistory> _passwordHistoryRepo;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ResetPasswordService> _logger;
        private readonly PasswordHasher<Users> _passwordHasher;

        public ResetPasswordService(
            UserManager<Users> userManager,
            IRepository<Users> userRepo,
            IRepository<Company> companyRepo,
            IRepository<UserPasswordHistory> passwordHistoryRepo,
            IMapper mapper,
            IConfiguration configuration,
            ILogger<ResetPasswordService> logger)
        {
            _userManager = userManager;
            _userRepo = userRepo;
            _companyRepo = companyRepo;
            _passwordHistoryRepo = passwordHistoryRepo;
            _mapper = mapper;
            _configuration = configuration;
            _logger = logger;
            _passwordHasher = new PasswordHasher<Users>();
        }

        public async Task<string> ChangePasswordAsync(ResetPasswordDto dto)
        {
            //  Use email from DTO
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return "User not found";

            // Fetch only last 5 password hashes directly from DB
            var last5 = await _passwordHistoryRepo.Query()
                .Where(p => p.UserId == user.Id)
                .OrderByDescending(p => p.ChangedOn)
                .Take(5)
                .ToListAsync();

            foreach (var past in last5)
            {
                if (_userManager.PasswordHasher.VerifyHashedPassword(user, past.PasswordHash, dto.NewPassword)
                    == PasswordVerificationResult.Success)
                {
                    return "You cannot reuse one of your last 5 passwords.";
                }
            }

            //  Change password through Identity
            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return errors;
            }

            // Log new password
            var newHistory = new UserPasswordHistory
            {
                UserId = user.Id,
                PasswordHash = user.PasswordHash,
                ChangedOn = DateTime.UtcNow
            };

            await _passwordHistoryRepo.AddAsync(newHistory);
            await _passwordHistoryRepo.SaveAsync();

            //  Clean up older history (keep only last 5)
            var oldPasswords = await _passwordHistoryRepo.Query()
                .Where(p => p.UserId == user.Id)
                .OrderByDescending(p => p.ChangedOn)
                .Skip(5)
                .ToListAsync();

            if (oldPasswords.Any())
            {
                foreach (var old in oldPasswords)
                    _passwordHistoryRepo.Delete(old);

                await _passwordHistoryRepo.SaveAsync();
            }

            return "Password changed successfully";
        }


        public async Task<string> ChangeInitialPasswordAsync(ResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return "Email is required.";
            }

            var user = await _userRepo.GetSingleAsync(u => u.Email.ToLower() == dto.Email.ToLower());
            if (user == null)
            {

                return "User not found.";
            }

            if (!user.IsApproved)
            {

                return "Your account is not approved yet. Please wait for Superadmin approval.";
            }

            var passwordVerification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword);
            if (passwordVerification == PasswordVerificationResult.Failed)
            {

                return "Old password is incorrect.";
            }

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
            {

                return "New password must be at least 8 characters long.";
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
            user.IsFirstLogin = false;

            _userRepo.Update(user);
            await _userRepo.SaveAsync();

            return "Password changed successfully.";
        }

    }
}

