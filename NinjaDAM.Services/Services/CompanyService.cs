using AutoMapper;
using Microsoft.AspNetCore.Identity;
using NinjaDAM.Services.IServices;
using NinjaDAM.Entity.Entities;
using NinjaDAM.Entity.IRepositories;
using NinjaDAM.DTO.Company;
using NinjaDAM.DTO.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NinjaDAM.Services.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly IRepository<Company> _companyRepo;
        private readonly IMapper _mapper;
        private readonly UserManager<Users> _userManager;

        public CompanyService(
            IRepository<Company> companyRepo,
            IMapper mapper,
            UserManager<Users> userManager)
        {
            _companyRepo = companyRepo;
            _mapper = mapper;
            _userManager = userManager;
        }

        // Get all companies
        public async Task<IEnumerable<CompanyDto>> GetAllAsync()
        {
            var companies = await _companyRepo.GetAllAsync();
            return _mapper.Map<IEnumerable<CompanyDto>>(companies);
        }

        // Get company by ID
        public async Task<CompanyDto?> GetByIdAsync(Guid id)
        {
            var company = await _companyRepo.GetByIdAsync(id);
            return company == null ? null : _mapper.Map<CompanyDto>(company);
        }

        // Get all users for a specific company
        public async Task<IEnumerable<CompanyUserResponseDto>> GetUsersByCompanyIdAsync(Guid companyId)
        {
            // Get users for the company
            var users = _userManager.Users.Where(u => u.CompanyId == companyId).ToList();

            var result = new List<CompanyUserResponseDto>();

            foreach (var user in users)
            {
                // Get roles for the user
                var roles = await _userManager.GetRolesAsync(user);  // returns IList<string>
                var role = roles.FirstOrDefault() ?? "No role";

                // Get company name
                var company = await _companyRepo.GetByIdAsync(companyId);

                result.Add(new CompanyUserResponseDto
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Role = role,
                    CompanyName = company?.CompanyName ?? string.Empty
                });
            }

            return result;
        }

    }
}
