using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CiMYTict.Models
{
    public class Showtime
    {
        public int ShowtimeId { get; set; }

        public int MovieId { get; set; }
        public Movie? Movie { get; set; }

        public int CinemaHallId { get; set; }
        public CinemaHall? CinemaHall { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        [Range(0.01, 1000)]
        [Column(TypeName = "decimal(10,2)")]
        public decimal BaseTicketPrice { get; set; }

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
