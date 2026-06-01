using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using user_and_identity_management.Models;
using user_and_identity_management.Models.Authentication.SignUp;

namespace user_and_identity_management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;

        public AuthenticationController(UserManager<IdentityUser> userManager, 
            RoleManager<IdentityRole> roleManager, IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
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
                return StatusCode(StatusCodes.Status201Created,
                        new Response
                        {
                            Status = "Success",
                            Message = "User Created Successfully"
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

    }
}
