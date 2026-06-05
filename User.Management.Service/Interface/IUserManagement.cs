using User.Management.Service.Models;
using User.Management.Service.Models.Authentication.SignUp;

namespace User.Management.Service.Interface
{
    public interface IUserManagement
    {
        Task<ApiResponse<string>> CreateUserWithTokenAsync(RegisterUser registerUser);
    }
}
