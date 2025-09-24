// DTOs/Auth/RegisterDto.cs
using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages;

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
        public bool? HasMedicalConditions { get; set; }          // nullable so it’s optional for >=16
        public List<RegistrationMedicalConditionDto>? MedicalConditions { get; set; } // required iff HasMedicalConditions==true
    }
    public class RegistrationMedicalConditionDto
    {
        public string Name { get; set; } = default!;
        public string? Notes { get; set; }
        public bool IsAllergy { get; set; }
    }
}
