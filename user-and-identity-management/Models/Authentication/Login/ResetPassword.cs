using System.ComponentModel.DataAnnotations;

namespace user_and_identity_management.Models.Authentication.Login
{
    public class ResetPassword
    {
        [Required]
        public string Password { get; set; }

        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string Token { get; set; } = null!;
    }
}
