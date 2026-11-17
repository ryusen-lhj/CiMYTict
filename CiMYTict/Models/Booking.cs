using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CiMYTict.Models
{
    public class Booking
    {
        public int BookingId { get; set; }

        [Required, StringLength(20)]
        public string BookingReference { get; set; } = string.Empty; // e.g., "CMT-20261117-A1B2C3"

        public int UserId { get; set; }
        public User? User { get; set; }

        public int ShowtimeId { get; set; }
        public Showtime? Showtime { get; set; }

        public int? PromotionId { get; set; }
        public Promotion? Promotion { get; set; }

        public int PointsRedeemed { get; set; } = 0;

        // Loyalty points awarded for this booking (reversed on cancellation)
        public int PointsEarned { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal DiscountAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Module 5: Temporary seat lock expiration (e.g., 10 mins from creation)
        public DateTime? LockExpiresAt { get; set; }

        // Module 6: Base64 QR Code image string for E-Ticket
        public string? QrCodeBase64 { get; set; }

        public ICollection<BookingSeat> BookingSeats { get; set; } = new List<BookingSeat>();
        public Payment? Payment { get; set; }
    }
}
