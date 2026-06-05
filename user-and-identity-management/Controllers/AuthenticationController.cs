using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using User.Management.Service.Models;
using User.Management.Service.Services;
using user_and_identity_management.Models;
using user_and_identity_management.Models.Authentication;
using user_and_identity_management.Models.Authentication.Login;
using user_and_identity_management.Models.Authentication.SignUp;

namespace user_and_identity_management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public AuthenticationController(UserManager<IdentityUser> userManager, 
            RoleManager<IdentityRole> roleManager, IEmailService emailService, IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _emailService = emailService;
            _configuration = configuration;
        }


        [HttpPost]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUser registerUser, string role)
        {
            var userExist = await _userManager.FindByNameAsync(registerUser.UserName);
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (userExist != null)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new Response
                    {
                        Status = "Error",
                        Message = "User already exists!"
                    });
            }

            IdentityUser user = new()
            {
                Email = registerUser.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = registerUser.UserName
            };

            if (await _roleManager.RoleExistsAsync(role))
            {

                var result = await _userManager.CreateAsync(user, registerUser.Password);
                if (!result.Succeeded)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response
                        {
                            Status = "Error",
                            Message = "User creation failed! Please check user details and try again."
                        });
                }

                await _userManager.AddToRoleAsync(user, role);

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = Url.Action(nameof(ConfirmEmail), "Authentication", new { token, email = user.Email }, Request.Scheme);
                var linkText = $"Please confirm your email by <a href='{confirmationLink}'>clicking here</a>.";
                var message = new Message(new string[] { user.Email }, "Email Confirmation", linkText);
                _emailService.SendEmail(message);

                return StatusCode(StatusCodes.Status201Created,
                        new Response
                        {
                            Status = "Success",
                            Message = "User Created and Email Confirmation Sent to " + user.Email + " successfully"
                        });
            }
            else
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response
                        {
                            Status = "Error",
                            Message = "This Role does not exist"
                        });
            }
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string token, string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                var result = await _userManager.ConfirmEmailAsync(user, token);
                if (result.Succeeded)
                {
                    return StatusCode(StatusCodes.Status200OK,
                        new Response
                        {
                            Status = "Success",
                            Message = "Email confirmed successfully!"
                        });
                }
            }
            return StatusCode(StatusCodes.Status500InternalServerError,
                        new Response
                        {
                            Status = "Error",
                            Message = "Email confirmation failed!"
                        });
        }

        [Authorize]
        [HttpPost("enable-2fa")]
        public async Task<IActionResult> Enable2FA()
        {
            var user =
                await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            await _userManager.SetTwoFactorEnabledAsync(
                user,
                true
            );

            return Ok(new Response
            {
                Status = "Success",
                Message = "Two-factor authentication enabled"
            });
        }

        [Authorize]
        [HttpPost("disable-2fa")]
        public async Task<IActionResult> Disable2FA()
        {
            var user = await _userManager.FindByNameAsync(User.Identity!.Name);

            if (user == null)
            {
                return Unauthorized();
            }

            await _userManager.SetTwoFactorEnabledAsync(
                user,
                false
            );

            return Ok(new Response
            {
                Status = "Success",
                Message = "Two-factor authentication disabled"
            });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOTP model)
        {
            var user =
                await _userManager.FindByNameAsync(
                    model.Username
                );

            if (user == null)
            {
                return Unauthorized();
            }

            var isValid =
                await _userManager.VerifyTwoFactorTokenAsync(
                    user,
                    TokenOptions.DefaultEmailProvider,
                    model.Code
                );

            if (!isValid)
            {
                return BadRequest(new Response
                {
                    Status = "Error",
                    Message = "Invalid OTP"
                });
            }

            var authClaims = new List<Claim>
            {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var userRoles =
                await _userManager.GetRolesAsync(user);

            foreach (var role in userRoles)
            {
                authClaims.Add(
                    new Claim(
                        ClaimTypes.Role,
                        role
                    )
                );
            }

            var jwtToken = GetToken(authClaims);

            return Ok(new
            {
                token =
                    new JwtSecurityTokenHandler()
                        .WriteToken(jwtToken),

                expiration = jwtToken.ValidTo
            });
        }

        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> LogIn([FromBody] UserLogin userLogin)
        {
            var user = await _userManager.FindByNameAsync(userLogin.Username);

            if(user != null && await _userManager.CheckPasswordAsync(user, userLogin.Password))
            {
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

                if(user.TwoFactorEnabled)
                {
                    if (!await _userManager.IsEmailConfirmedAsync(user))
                    {
                        return Unauthorized(
                            new Response
                            {
                                Status = "Error",
                                Message = "Please confirm your email first."
                            });
                    }

                    var otp = await _userManager.GenerateTwoFactorTokenAsync(user,TokenOptions.DefaultEmailProvider );
                    var message = new Message(
                        new[] { user.Email },
                        "Login Verification Code",
                        $"Your verification code is {otp}"
                    );

                    _emailService.SendEmail(message);

                    return Ok(new Response
                    {
                        Status = "Success",
                        Message = $"OTP sent to {user.Email}"
                    });
                }

                var jwtToken = GetToken(authClaims);


                return Ok(new
                {
                    token = new JwtSecurityTokenHandler().WriteToken(jwtToken),
                    expiration = jwtToken.ValidTo
                });


            }
            return Unauthorized();
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
