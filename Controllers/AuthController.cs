using CorpsAPI.DTOs;
using CorpsAPI.Models;
using CorpsAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace CorpsAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private const string serverUrl = "https://localhost:7125";
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly EmailService _emailService;

        public AuthController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, EmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
        {
            var user = new AppUser 
            { 
                UserName = dto.UserName, 
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                DateOfBirth = dto.DateOfBirth,
            };
            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded) return BadRequest(result.Errors);

            // generate email verification token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var confirmationUrl = $"{serverUrl}/api/auth/confirm-email?userId={user.Id}&token={encodedToken}";

            // send email
            await _emailService.SendEmailAsync(user.Email, "Verify your email", 
                $"Confirm your email:\n<a href='{confirmationUrl}'>Click Here!</a>");

            return Ok("Registration successful. Please check your email to confirm your account.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            // check email
            if (user == null) return Unauthorized(new
            {
                message = "Invalid login credentials.",
                canResend = false
            });

            // check if email is verified
            // TODO: app looks for 401 response, checks for canSend value, and displays a link to the resend-verification endpoint
            if (!user.EmailConfirmed) return Unauthorized(new
            {
                message = "Email not confirmed. Please check your inbox/spam folders for the verification link.",
                canResend = true
            });

            // check password
            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded) return Unauthorized(new
            {
                message = "Invalid login credentials.",
                canResend = false
            });

            // TODO: Generate JWT token and return it
            return Ok("Login successful (token generation next)");
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found.");

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded) return Ok("Email successfully confirmed.");
            else return BadRequest("Email confirmation failed.");
        }

        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendEmailDTO dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return NotFound("Invalid email address");
            if (user.EmailConfirmed) return BadRequest("Email is already confirmed");

            // generate email verification token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var confirmationUrl = $"{serverUrl}/api/auth/confirm-email?userId={user.Id}&token={encodedToken}";

            // send email
            // TODO: implement rate limiting, five mins per email per user.
            await _emailService.SendEmailAsync(user.Email, "Verify your email",
                $"Confirm your email:\n<a href='{confirmationUrl}'>Click Here!</a>");

            return Ok("Verificiation email resent. Please check your inbox/spam folders.");
        }
    }

}
