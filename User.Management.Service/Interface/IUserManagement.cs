using Microsoft.AspNetCore.Identity;
using User.Management.Service.Models;
using User.Management.Service.Models.Authentication.Login;
using User.Management.Service.Models.Authentication.SignUp;
using User.Management.Service.Models.Authentication.User;

namespace User.Management.Service.Interface
{
    public interface IUserManagement
    {
        Task<ApiResponse<CreateUserResponse>> CreateUserWithTokenAsync(RegisterUser registerUser);

        Task<ApiResponse<List<string>>> AssignRoleToUserAsync(List<string> roles, IdentityUser user);

        Task<ApiResponse<string>> LogInUserAsync(UserLogin userLogin);

        Task<string> GenerateTokenStringAsync(IdentityUser user);

        Task<ApiResponse<string>> ConfirmEmailAsync(string token, string email);
        Task<ApiResponse<string>> VerifyOtpAsync(VerifyOTP model);
        Task<ApiResponse<string>> UpdateTwoFactorAsync(string username, bool enabled);
        Task<ApiResponse<string>> ForgotPasswordAsync(string email);
        Task<ApiResponse<IdentityResult>> ResetPasswordAsync(ResetPassword resetPassword);
    }
}
