using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CiMYTict.Models
{
    public class CinemaHall
    {
        public int CinemaHallId { get; set; }

        [Required, StringLength(50)]
        public string HallName { get; set; } = string.Empty; // e.g., "Hall 1 - IMAX"

        public CinemaHallType HallType { get; set; } = CinemaHallType.StandardMidSize;

        [Range(1, 26, ErrorMessage = "Rows must be between 1 and 26 (A-Z).")]
        public int TotalRows { get; set; }

        [Range(1, 30)]
        public int SeatsPerRow { get; set; }

        public int Capacity => TotalRows * SeatsPerRow;

        public ICollection<Seat> Seats { get; set; } = new List<Seat>();
        public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
    }

}