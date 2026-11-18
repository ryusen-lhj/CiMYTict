using System.ComponentModel.DataAnnotations;

namespace CiMYTict.ViewModels
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, Compare(nameof(Password)), DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}