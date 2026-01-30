using NinjaDAM.DTO.User;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NinjaDAM.DTO.login
{
    public class LoginRequestDto
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; }
    }

    public class LoginResponseDto
    {
        public UserDto? User { get; set; }

        
        public bool RequirePasswordReset { get; set; } = false;

      
        public string? Message { get; set; }
    }
}
