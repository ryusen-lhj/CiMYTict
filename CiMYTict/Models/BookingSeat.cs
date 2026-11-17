using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CiMYTict.Models
{
    public class BookingSeat
    {
        public int BookingSeatId { get; set; }

        public int BookingId { get; set; }
        public Booking? Booking { get; set; }

        public int SeatId { get; set; }
        public Seat? Seat { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal SeatPrice { get; set; }
    }
}