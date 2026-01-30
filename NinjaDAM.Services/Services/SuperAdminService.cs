using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NinjaDAM.Services.IServices;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.DTO.SuperAdmin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NinjaDAM.Services.Services
{
    public class SuperAdminService : ISuperAdminService
    {
        private readonly IRepository<Users> _userRepo;
        private readonly IRepository<Company> _companyRepo;
        private readonly IEmailService _emailService;
        private readonly IMapper _mapper;
        private readonly ILogger<SuperAdminService> _logger;

        public SuperAdminService(
            IRepository<Users> userRepo,
            IRepository<Company> companyRepo,
            IEmailService emailService,
            IMapper mapper,
            ILogger<SuperAdminService> logger)
        {
            _userRepo = userRepo;
            _companyRepo = companyRepo;
            _emailService = emailService;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<IEnumerable<PendingUserDto>> GetPendingUsersAsync()
        {
            var users = await _userRepo
                .Query()
                .Where(u => !u.IsApproved)
                .Include(u => u.Company)
                .ToListAsync();

            return _mapper.Map<IEnumerable<PendingUserDto>>(users);
        }

        public async Task<string> ApproveUserAsync(Guid userId)
        {
            var user = await _userRepo.GetByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogError("ApproveUserAsync: User with ID {UserId} not found", userId);
                return "User not found.";
            }

            user.IsApproved = true;
            user.IsActive = true;

            _userRepo.Update(user);
            await _userRepo.SaveAsync();

            var placeholders = new Dictionary<string, string>
            {
                { "{{FirstName}}", user.FirstName },
                { "{{LastName}}", user.LastName },
                { "{{Email}}", user.Email },
                { "{{Year}}", DateTime.Now.Year.ToString() }
            };

            await _emailService.SendTemplatedEmailAsync(
               user.Email,
               "Welcome to NinjaDAM! Your Account Details",
               "Approval",
               placeholders
            );

            return "User approved successfully.";
        }

       
        public async Task<string> RejectUserAsync(Guid userId)
        {
           
                var user = await _userRepo.GetByIdAsync(userId.ToString());
                if (user == null)
                {
                    _logger.LogError("RejectUserAsync: User with ID {UserId} not found", userId);
                    return "User not found.";
                }

                user.IsApproved = false;
                user.IsActive = false;

                _userRepo.Update(user);
                await _userRepo.SaveAsync();

                var placeholders = new Dictionary<string, string>
                {
                    { "{{FirstName}}", user.FirstName },
                    { "{{LastName}}", user.LastName },
                    { "{{Email}}", user.Email },
                    { "{{Year}}", DateTime.Now.Year.ToString() }
                };

                await _emailService.SendTemplatedEmailAsync(
                   user.Email,
                   "Welcome to NinjaDAM! Your Account Details",
                   "Rejection",
                   placeholders
                );

                return "User rejected successfully.";
        }
           

        
    }
}
