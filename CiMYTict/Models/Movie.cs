using System.ComponentModel.DataAnnotations;

namespace CiMYTict.Models
{
    public class Movie
    {
        public int MovieId { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(80)]
        public string Genre { get; set; } = string.Empty;

        [Required, StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Range(30, 300)]
        public int DurationMinutes { get; set; }

        [StringLength(10)]
        public string AgeRating { get; set; } = "PG-13"; // e.g., U, P13, 18

        [DataType(DataType.Date)]
        public DateTime ReleaseDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? TheaterEndDate { get; set; }

        public string? PosterUrl { get; set; }   // Stored in /uploads/posters/
        public string? TrailerUrl { get; set; }  // YouTube embed URL

        public bool IsActive { get; set; } = true;

        // Navigation
        public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
    }
}