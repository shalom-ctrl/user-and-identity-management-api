using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Data;
using User.Management.Service.Interface;
using User.Management.Service.Models;
using User.Management.Service.Models.Authentication.SignUp;
using User.Management.Service.Models.Authentication.User;

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

        public async Task<ApiResponse<List<string>>> AssignRoleToUserAsync(List<string> roles, IdentityUser user)
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

            IdentityUser user = new()
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
    }
}
