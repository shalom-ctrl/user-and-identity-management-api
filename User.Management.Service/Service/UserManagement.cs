using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Data;
using User.Management.Service.Interface;
using User.Management.Service.Models;
using User.Management.Service.Models.Authentication.SignUp;

namespace User.Management.Service.Service
{
    public class UserManagement : IUserManagement
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserManagement(UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<ApiResponse<string>> CreateUserWithTokenAsync(RegisterUser registerUser)
        {
            var userExist = await _userManager.FindByNameAsync(registerUser.UserName);

            if (userExist != null)
            {
                return new ApiResponse<string>
                {
                    IsSuccess = false,
                    Message = "User already exists!",
                    StatusCode = StatusCodes.Status403Forbidden    
                };
            }

            IdentityUser user = new()
            {
                Email = registerUser.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = registerUser.UserName
            };

            if (await _roleManager.RoleExistsAsync(registerUser.Role))
            {

                var result = await _userManager.CreateAsync(user, registerUser.Password);
                if (!result.Succeeded)
                {
                    return new ApiResponse<string>
                    {
                        IsSuccess = false,
                        Message = "User creation failed! Please check user details and try again.",
                        StatusCode = StatusCodes.Status500InternalServerError
                    };
                }

                await _userManager.AddToRoleAsync(user, registerUser.Role);

                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                return new ApiResponse<string>
                {
                    IsSuccess = true,
                    Message = "User created successfully!",
                    StatusCode = StatusCodes.Status201Created,
                    Response = token
                };
            }
            else
            {
                return new ApiResponse<string>
                {
                    IsSuccess = false,
                    Message = "Role does not exist!",
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }
        }
    }
}
