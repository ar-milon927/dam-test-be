using Microsoft.AspNetCore.Mvc;
using NinjaDAM.Services.IServices;
using NinjaDAM.DTO.Company;
using NinjaDAM.DTO.User;
using System;
using System.Threading.Tasks;

namespace NinjaDAM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CompanyController : ControllerBase
    {
        private readonly ICompanyService _companyService;

        public CompanyController(ICompanyService companyService)
        {
            _companyService = companyService;
        }

        // GET: api/company
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var companies = await _companyService.GetAllAsync();
            return Ok(companies);
        }

        // GET: api/company/{id}  
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var company = await _companyService.GetByIdAsync(id);
            if (company == null) return NotFound();
            return Ok(company);
        }

        // GET: api/company/{id}/users     
        [HttpGet("users")]
        public async Task<IActionResult> GetUsersByCompanyId(Guid companyId)
        {
            var users = await _companyService.GetUsersByCompanyIdAsync(companyId);
            return Ok(users);
        }
    }
}
