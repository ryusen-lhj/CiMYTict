using System.ComponentModel.DataAnnotations;

namespace CiMYTict.Models
{
    public class User
    {
        public int UserId { get; set; }

        [Required, StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Phone, StringLength(20)]
        public string? PhoneNumber { get; set; }

        public UserRole Role { get; set; } = UserRole.Customer;

        // Module 7: Membership Rewards
        public int RewardPoints { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
