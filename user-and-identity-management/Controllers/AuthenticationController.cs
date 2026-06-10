using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using User.Management.Service.Interface;
using User.Management.Service.Models;
using User.Management.Service.Models.Authentication.Login;
using User.Management.Service.Models.Authentication.SignUp;
using User.Management.Service.Services;
using user_and_identity_management.Models;

namespace user_and_identity_management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IUserManagement _userManagement;

        public AuthenticationController(IEmailService emailService, IConfiguration configuration,
            IUserManagement userManagement)
        {
            _emailService = emailService;
            _configuration = configuration;
            _userManagement = userManagement;
        }


        [HttpPost]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUser registerUser)
        {
            var serviceResult = await _userManagement.CreateUserWithTokenAsync(registerUser);

            if (!serviceResult.IsSuccess)
            {
                return StatusCode(serviceResult.StatusCode,
                    new Response { Status = "Error", Message = serviceResult.Message });
            }
            var rawToken = serviceResult.Response.Token;
            var encodedToken = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(rawToken));

            await _userManagement.AssignRoleToUserAsync(registerUser.Roles, serviceResult.Response.User);
            var confirmationLink = Url.Action(nameof(ConfirmEmail), "Authentication", new { token = encodedToken, email = registerUser.Email }, Request.Scheme);
            var linkText = $"Please confirm your email by <a href='{confirmationLink}'>clicking here</a>.";
            var message = new Message(new string[] { registerUser.Email }, "Email Confirmation", linkText);
            _emailService.SendEmail(message);

            return StatusCode(StatusCodes.Status201Created,
                        new Response
                        {
                            Status = "Success",
                            Message = "User created successfully! Please check your email to confirm your account."
                        });
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string token, string email)
        {
            var serviceResult = await _userManagement.ConfirmEmailAsync(token, email);
            if (!serviceResult.IsSuccess)
            {
                return StatusCode(serviceResult.StatusCode, new Response { Status = "Error", Message = serviceResult.Message });
            }

            return StatusCode(serviceResult.StatusCode, new Response { Status = "Success", Message = serviceResult.Message });
        }

        [Authorize]
        [HttpPost("enable-2fa")]
        public async Task<IActionResult> Enable2FA()
        {
            var serviceResult = await _userManagement.UpdateTwoFactorAsync(User.Identity!.Name, true);
            if (!serviceResult.IsSuccess)
            {
                return StatusCode(serviceResult.StatusCode, new Response { Status = "Error", Message = serviceResult.Message });
            }

            return Ok(new Response { Status = "Success", Message = serviceResult.Message });
        }

        [Authorize]
        [HttpPost("disable-2fa")]
        public async Task<IActionResult> Disable2FA()
        {
            var serviceResult = await _userManagement.UpdateTwoFactorAsync(User.Identity!.Name, false);
            if (!serviceResult.IsSuccess)
            {
                return StatusCode(serviceResult.StatusCode, new Response { Status = "Error", Message = serviceResult.Message });
            }

            return Ok(new Response { Status = "Success", Message = serviceResult.Message });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOTP model)
        {
            var serviceResult = await _userManagement.VerifyOtpAsync(model);
            if (!serviceResult.IsSuccess)
            {
                return StatusCode(serviceResult.StatusCode, new Response { Status = "Error", Message = serviceResult.Message });
            }
            var tokenString = serviceResult.Response;
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(tokenString);
            return Ok(new
            {
                token = tokenString,
                expiration = jwtToken.ValidTo
            });
        }
        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> LogIn([FromBody] UserLogin userLogin)
        {
            var serviceResult = await _userManagement.LogInUserAsync(userLogin);
            if (!serviceResult.IsSuccess)
            {
                return StatusCode(serviceResult.StatusCode, new Response { Status = "Error", Message = serviceResult.Message });
            }

            if (serviceResult.Response != null)
            {
                return Ok(new { token = serviceResult.Response });
            }

            return Ok(new Response { Status = "Success", Message = serviceResult.Message });
        }

        [HttpPost("Forgot-Password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([Required] string email)
        {
            var serviceResult = await _userManagement.ForgotPasswordAsync(email);
            if (!serviceResult.IsSuccess)
            {
                return StatusCode(serviceResult.StatusCode, new Response { Status = "Error", Message = serviceResult.Message });
            }

            var forgotPasswordLink = Url.Action(nameof(ResetPassword), "Authentication", new { token = serviceResult.Response, email }, Request.Scheme);
            var resetPasswordLinkText = $"Please reset your password by <a href='{forgotPasswordLink}'>clicking here</a>.";
            var message = new Message(new string[] { email }, "Reset Password Email", resetPasswordLinkText);
            _emailService.SendEmail(message);

            return StatusCode(StatusCodes.Status200OK, new Response { Status = "Success", Message = $"Password reset link sent to {email} successfully" });
        }

        [HttpGet("Reset-Password")]
        public async Task<IActionResult> ResetPassword(string token, string email)
        {
            var model = new ResetPassword { Token = token, Email = email };
            return Ok(new { model });
        }

        [HttpPost("Reset-Password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPassword resetPassword)
        {
            var serviceResult = await _userManagement.ResetPasswordAsync(resetPassword);
            if (!serviceResult.IsSuccess)
            {
                if (serviceResult.Response != null)
                {
                    foreach (var error in serviceResult.Response.Errors)
                    {
                        ModelState.AddModelError(error.Code, error.Description);
                    }
                    return Ok(ModelState);
                }
                return StatusCode(serviceResult.StatusCode, new Response { Status = "Error", Message = serviceResult.Message });
            }

            return StatusCode(StatusCodes.Status200OK, new Response { Status = "Success", Message = "Password reset successfully" });
        }

    }
}
