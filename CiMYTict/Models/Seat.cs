using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CiMYTict.Models
{
    public class Seat
    {
        public int SeatId { get; set; }

        public int CinemaHallId { get; set; }
        public CinemaHall? CinemaHall { get; set; }

        [Required, StringLength(5)]
        public string RowLabel { get; set; } = string.Empty; // "A", "B", "C"

        public int SeatNumber { get; set; } // 1, 2, 3...

        public string SeatCode => $"{RowLabel}{SeatNumber}";

        public SeatCategory Category { get; set; } = SeatCategory.Standard;

        [Column(TypeName = "decimal(10,2)")]
        public decimal ExtraPrice { get; set; } = 0.00m; // Surcharge for VIP/Couple
    }
}