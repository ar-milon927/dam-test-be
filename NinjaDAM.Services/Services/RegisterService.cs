using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NinjaDAM.DTO.Register;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;

namespace NinjaDAM.Services.Services
{
    public class RegisterService : IRegisterService
    {
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMapper _mapper;
        private readonly IRepository<Company> _companyRepository;
        private readonly IEmailService _emailService; 
        private readonly IPermissionService _permissionService;

        public RegisterService(
            UserManager<Users> userManager,
            RoleManager<IdentityRole> roleManager,
            IMapper mapper,
            IRepository<Company> companyRepository,
            IEmailService emailService,
            IPermissionService permissionService) 
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _mapper = mapper;
            _companyRepository = companyRepository;
            _emailService = emailService;
            _permissionService = permissionService;
        }

        public async Task<object> RegisterAsync(RegisterDto dto)
        {
            if (dto == null)
                return new { message = "Invalid registration data." };

            // Check if a user with the same email already exists    
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                return new { message = "Email already registered." };

            Company? company = null;

            // Handle company association
            if (!string.IsNullOrWhiteSpace(dto.CompanyName))
            {
                string companyNameNormalized = dto.CompanyName.Trim().ToLower();

                company = await _companyRepository.GetSingleAsync(
                    c => c.CompanyName.ToLower() == companyNameNormalized
                );

                if (company == null)
                {
                    // Create a new company
                    company = _mapper.Map<Company>(dto);
                    company.IsActive = true;

                    await _companyRepository.AddAsync(company);
                    await _companyRepository.SaveAsync();
                }
            }

            // Map DTO to user entity         
            var user = _mapper.Map<Users>(dto);

            // Assign company ID if available
            if (company != null)
                user.CompanyId = company.Id;

            // Generate temporary password for first login
            var tempPassword = GenerateTempPassword();

            // Set account flags
            user.IsActive = true;       
            user.IsApproved = true;    
            user.IsFirstLogin = true;    

            // Create user with temporary password 
            var result = await _userManager.CreateAsync(user, tempPassword);
            if (!result.Succeeded)
                return new { message = string.Join(", ", result.Errors.Select(e => e.Description)) };

            // Get all valid roles
            var validRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

            // Validate role from DTO 
            string selectedRole = ValidateRole(dto.Role, validRoles);

            // Assign role to the user
            await _userManager.AddToRoleAsync(user, selectedRole);

            // AUTO-ASSIGN PERMISSIONS BASED ON ROLE
            try
            {
                await _permissionService.AssignDefaultPermissionsAsync(user.Id, selectedRole);
            }
            catch
            {
                // Soft fail for registration permission assignment
            }
            // Send temporary password to user email 
            var placeholders = new Dictionary<string, string>
                        {
                            { "{{FirstName}}", user.FirstName },
                            { "{{LastName}}", user.LastName },
                            { "{{Email}}", user.Email },
                            { "{{TemporaryPassword}}", tempPassword },
                            { "{{Year}}", DateTime.Now.Year.ToString() }
                        };

            await _emailService.SendTemplatedEmailAsync(
                user.Email,
                "Welcome to NinjaDAM! Your Account Details",
                "Registration",
                placeholders
            );

            return new
            {
                message = $"User registered successfully with role '{selectedRole}'.",
                email = dto.Email,
                companyName = dto.CompanyName,
                storageTier = company?.StorageTier ?? "Not selected",
            };
        }

        /// <summary>
        /// Validates the provided role name against existing roles and falls back to 'User'.  
        /// </summary>
        private static string ValidateRole(string? role, List<string?> validRoles)
        {
            if (string.IsNullOrWhiteSpace(role) || role.Equals("string", StringComparison.OrdinalIgnoreCase))
                return "User"; // Default role

            return validRoles.Contains(role, StringComparer.OrdinalIgnoreCase) ? role : "User";
        }


        private string GenerateTempPassword(int length = 8)
        {
            if (length < 4) length = 8; // Ensure minimum length  

            const string lower = "abcdefghijklmnopqrstuvwxyz";
            const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string special = "!@#$%^&*";

            var random = new Random();

            // Ensure at least one of each required type
            char lowerChar = lower[random.Next(lower.Length)];
            char upperChar = upper[random.Next(upper.Length)];
            char digitChar = digits[random.Next(digits.Length)];
            char specialChar = special[random.Next(special.Length)];

            // Fill remaining characters randomly from all sets    
            string allChars = lower + upper + digits + special;
            var remainingChars = new char[length - 4];
            for (int i = 0; i < remainingChars.Length; i++)
                remainingChars[i] = allChars[random.Next(allChars.Length)];

            // Combine all characters into a single array
            var passwordChars = new char[length];
            var requiredChars = new char[] { lowerChar, upperChar, digitChar, specialChar };

            requiredChars.CopyTo(passwordChars, 0);   // Add the 4 required characters
            remainingChars.CopyTo(passwordChars, 4);  // Add the remaining random characters   

            // Shuffle the array to randomize order
            return new string(passwordChars.OrderBy(c => random.Next()).ToArray());
        }


    }



}
