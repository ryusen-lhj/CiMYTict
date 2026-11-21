using System.ComponentModel.DataAnnotations;

namespace CiMYTict.Models
{
    public class PasswordResetCode
    {
        public int Id { get; set; }
        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string CodeHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; } = 0;
    }
}