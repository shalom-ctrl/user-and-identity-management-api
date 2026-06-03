using System.ComponentModel.DataAnnotations;

namespace user_and_identity_management.Models.Authentication.Login
{
    public class UserLogin
    {
        [Required(ErrorMessage = "Username is required")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public string? Password { get; set; }
    }
}
