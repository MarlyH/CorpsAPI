// DTOs/Auth/RegisterDto.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace CorpsAPI.DTOs.Auth
{
    public class RegisterDto
    {
        [Required]
        public string UserName { get; set; } = default!;
        [Required]
        public string Email { get; set; } = default!;
        [Required]
        public string Password { get; set; } = default!;
        [Required]
        public string FirstName { get; set; } = default!;
        [Required]
        public string LastName { get; set; } = default!;
        [Required]
        public DateOnly DateOfBirth { get; set; }
        [Required]
        public string PhoneNumber { get; set; } = default!;
    }
}
