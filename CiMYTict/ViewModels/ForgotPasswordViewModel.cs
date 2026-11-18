using System.ComponentModel.DataAnnotations;

namespace CiMYTict.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}