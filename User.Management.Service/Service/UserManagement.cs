using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using User.Management.Data.Models;
using User.Management.Service.Interface;
using User.Management.Service.Models;
using User.Management.Service.Models.Authentication.Login;
using User.Management.Service.Models.Authentication.SignUp;
using User.Management.Service.Models.Authentication.User;
using User.Management.Service.Services;

namespace User.Management.Service.Service
{
    public class UserManagement : IUserManagement
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public UserManagement(UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, IEmailService emailService, IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _emailService = emailService;
            _configuration = configuration;
        }

        public async Task<ApiResponse<List<string>>> AssignRoleToUserAsync(List<string> roles, ApplicationUser user)
        {
            var assignedRoles = new List<string>();
            foreach (var role in roles)
            {
                if (await _roleManager.RoleExistsAsync(role))
                {
                    if(!await _userManager.IsInRoleAsync(user, role))
                    {
                        await _userManager.AddToRoleAsync(user, role);
                        assignedRoles.Add(role);
                    }
                }
            }

            return new ApiResponse<List<string>>
            {
                IsSuccess = true,
                Message = "Roles assigned successfully!",
                StatusCode = StatusCodes.Status200OK,
                Response = assignedRoles
            };
        }

        public async Task<ApiResponse<CreateUserResponse>> CreateUserWithTokenAsync(RegisterUser registerUser)
        {
            var userExist = await _userManager.FindByNameAsync(registerUser.UserName);

            if (userExist != null)
            {
                return new ApiResponse<CreateUserResponse>
                {
                    IsSuccess = false,
                    Message = "User already exists!",
                    StatusCode = StatusCodes.Status403Forbidden    
                };
            }

            ApplicationUser user = new()
            {
                Email = registerUser.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = registerUser.UserName
            };

                var result = await _userManager.CreateAsync(user, registerUser.Password);
                if (!result.Succeeded)
                {
                    return new ApiResponse<CreateUserResponse>
                    {
                        IsSuccess = false,
                        Message = "User creation failed! Please check user details and try again.",
                        StatusCode = StatusCodes.Status500InternalServerError
                    };
                }

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                return new ApiResponse<CreateUserResponse>
                {
                    IsSuccess = true,
                    Message = "User created successfully!",
                    StatusCode = StatusCodes.Status201Created,
                    Response = new CreateUserResponse
                    {
                        Token = token,
                        User = user
                    }
                };
        }

        public async Task<string> GenerateTokenStringAsync(ApplicationUser user)
        {
            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
             };

            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var role in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            var jwtToken = GetToken(authClaims);
            return new JwtSecurityTokenHandler().WriteToken(jwtToken);
        }

        public async Task<ApiResponse<string>> LogInUserAsync(UserLogin userLogin)
        {
            var user = await _userManager.FindByNameAsync(userLogin.Username);

            // 1. Validate Credentials
            if (user == null || !await _userManager.CheckPasswordAsync(user, userLogin.Password))
            {
                return new ApiResponse<string> 
                { IsSuccess = false, 
                  Message = "Invalid credentials.", 
                  StatusCode = StatusCodes.Status401Unauthorized };
            }

            // 2. Global Email Guard
            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                return new ApiResponse<string> { IsSuccess = false,
                Message = "Please confirm your email first.",
                StatusCode = StatusCodes.Status401Unauthorized };
            }

            // 3. Handle 2FA Flow
            if (user.TwoFactorEnabled)
            {
                var otp = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);

                var message = new Message(new[] { user.Email }, "Login Verification Code", $"Your verification code is {otp}");
                _emailService.SendEmail(message);

                return new ApiResponse<string>
                {
                    IsSuccess = true,
                    Message = $"OTP sent to {user.Email}",
                    StatusCode = StatusCodes.Status200OK,
                    Response = null 
                };
            }

            // 4. Handle Standard JWT Generation Flow
            var authClaims = new List<Claim>
             {
                 new Claim(ClaimTypes.NameIdentifier, user.Id),
                 new Claim(ClaimTypes.Name, user.UserName),
                 new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
             };

            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var userRole in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, userRole));
            }

            // Generate the JWT token string using your private helper method 
            var jwtToken = GetToken(authClaims);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(jwtToken);

            return new ApiResponse<string>
            {
                IsSuccess = true,
                Message = "Login successful!",
                StatusCode = StatusCodes.Status200OK,
                Response = tokenString 
            };
        }

        public async Task<ApiResponse<string>> ConfirmEmailAsync(string token, string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return new ApiResponse<string> { IsSuccess = false, Message = "User not found.", StatusCode = StatusCodes.Status404NotFound };
            }

            string safeToken = token.Replace(" ", "+");
            var decodedTokenBytes = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(safeToken);
            var normalToken = Encoding.UTF8.GetString(decodedTokenBytes);

            var result = await _userManager.ConfirmEmailAsync(user, normalToken);
            if (!result.Succeeded)
            {
                return new ApiResponse<string> { IsSuccess = false, Message = "Email confirmation failed!", StatusCode = StatusCodes.Status400BadRequest };
            }

            return new ApiResponse<string> { IsSuccess = true, Message = "Email confirmed successfully!", StatusCode = StatusCodes.Status200OK };
        }

        public async Task<ApiResponse<string>> VerifyOtpAsync(VerifyOTP model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null)
            {
                return new ApiResponse<string> { IsSuccess = false, Message = "Unauthorized access.", StatusCode = StatusCodes.Status401Unauthorized };
            }

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider, model.Code);
            if (!isValid)
            {
                return new ApiResponse<string> { IsSuccess = false, Message = "Invalid OTP", StatusCode = StatusCodes.Status400BadRequest };
            }

            var tokenString = await GenerateTokenStringAsync(user);
            return new ApiResponse<string>
            {
                IsSuccess = true,
                Message = "OTP Verified successfully!",
                StatusCode = StatusCodes.Status200OK,
                Response = tokenString
            };
        }

        public async Task<ApiResponse<string>> UpdateTwoFactorAsync(string username, bool enabled)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                return new ApiResponse<string> { IsSuccess = false, Message = "User not found.", StatusCode = StatusCodes.Status401Unauthorized };
            }

            await _userManager.SetTwoFactorEnabledAsync(user, enabled);
            return new ApiResponse<string>
            {
                IsSuccess = true,
                Message = $"Two-factor authentication {(enabled ? "enabled" : "disabled")}",
                StatusCode = StatusCodes.Status200OK
            };
        }

        public async Task<ApiResponse<string>> ForgotPasswordAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return new ApiResponse<string> { IsSuccess = false, Message = "User with this email does not exist", StatusCode = StatusCodes.Status400BadRequest };
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            return new ApiResponse<string> { IsSuccess = true, Message = "Token generated successfully", Response = token, StatusCode = StatusCodes.Status200OK };
        }

        public async Task<ApiResponse<IdentityResult>> ResetPasswordAsync(ResetPassword resetPassword)
        {
            var user = await _userManager.FindByEmailAsync(resetPassword.Email);
            if (user == null)
            {
                return new ApiResponse<IdentityResult> { IsSuccess = false, Message = "User with this email does not exist", StatusCode = StatusCodes.Status400BadRequest };
            }

            var result = await _userManager.ResetPasswordAsync(user, resetPassword.Token, resetPassword.Password);
            return new ApiResponse<IdentityResult> { IsSuccess = result.Succeeded, Response = result, StatusCode = result.Succeeded ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest };
        }

        private JwtSecurityToken GetToken(List<Claim> authClaims)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:SecretKey"]));

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"],
                audience: _configuration["JWT:ValidAudience"],
                expires: DateTime.Now.AddHours(1),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
                );

            return token;
        }
    }
}
