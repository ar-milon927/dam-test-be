using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NinjaDAM.DTO.login;
using NinjaDAM.DTO.User;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.Services.IServices;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NinjaDAM.Services.Services
{
    public class LoginService : ILoginService
    {
        private readonly IRepository<Users> _userRepo;
        private readonly IRepository<Company> _companyRepo;
        private readonly IMapper _mapper;
        private readonly IConfiguration _configuration;
        private readonly UserManager<Users> _userManager;
        private readonly IRepository<UserGroup> _userGroupRepo;
        private readonly IRepository<Group> _groupRepo;
        private readonly PasswordHasher<Users> _passwordHasher;

        public LoginService(
            UserManager<Users> userManager,
            IRepository<Users> userRepo,
            IRepository<Company> companyRepo,
            IRepository<UserGroup> userGroupRepo,
            IRepository<Group> groupRepo,
            IMapper mapper,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _userRepo = userRepo;
            _companyRepo = companyRepo;
            _userGroupRepo = userGroupRepo;
            _groupRepo = groupRepo;
            _mapper = mapper;
            _configuration = configuration;
            _passwordHasher = new PasswordHasher<Users>();
        }

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto loginDto)
        {
            if (loginDto == null ||
                string.IsNullOrWhiteSpace(loginDto.Email) ||
                string.IsNullOrWhiteSpace(loginDto.Password))
                return null;

            // Fetch user by email
            var user = await _userRepo.GetSingleAsync(u => u.Email.ToLower() == loginDto.Email.ToLower());
            if (user == null)
                return new LoginResponseDto { Message = "Invalid email or username." };

            // Check if account is approved
            if (!user.IsApproved)
                return new LoginResponseDto { Message = "Your account is pending Superadmin approval." };

            // Check if account is active
            if (!user.IsActive)
                return new LoginResponseDto { Message = "Your account is inactive. Contact Superadmin." };

            // Verify password
            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, loginDto.Password);
            if (verificationResult == PasswordVerificationResult.Failed)
                return new LoginResponseDto { Message = "Invalid or Incorrect password." };

            // Check first login: require password reset if true
            if (user.IsFirstLogin)
            {
                return new LoginResponseDto
                {
                    RequirePasswordReset = true,
                    Message = "You must reset your password before continuing."
                };
            }

            // Get roles
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "User";

            // Fetch company name
            string companyName = null;
            if (user.CompanyId.HasValue && user.CompanyId != Guid.Empty)
            {
                var company = await _companyRepo.GetByIdAsync(user.CompanyId.Value);
                companyName = company?.CompanyName;
            }

            // Create JWT claims
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.Role, role),
                new Claim("CompanyName", companyName ?? string.Empty),
                new Claim("CompanyId", user.CompanyId?.ToString() ?? string.Empty)
            };

            // Generate JWT token
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["Jwt:DurationInMinutes"])),
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            // Map user to DTO
            var userDto = _mapper.Map<UserDto>(user);
            userDto.Token = tokenString;
            userDto.Role = role;
            userDto.CompanyName = companyName;

            var userGroup = await _userGroupRepo.GetSingleAsync(ug => ug.UserId == user.Id);
            if (userGroup != null)
            {
                userDto.GroupId = userGroup.GroupId;
                var group = await _groupRepo.GetByIdAsync(userGroup.GroupId);
                userDto.GroupName = group?.Name;
            }

            return new LoginResponseDto
            {
                User = userDto,
                Message = "Login successful."
            };
        }

    }
}
