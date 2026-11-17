using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CiMYTict.Models
{
    public class Payment
    {
        public int PaymentId { get; set; }

        public int BookingId { get; set; }
        public Booking? Booking { get; set; }

        [Required, StringLength(50)]
        public string PaymentMethod { get; set; } = "Credit Card"; // Card, E-Wallet, Online Banking

        [Required, StringLength(100)]
        public string TransactionId { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal AmountPaid { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Completed;
        public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    }
}