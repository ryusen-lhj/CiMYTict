using CiMYTict.Services;
using CiMYTict.Data;
using CiMYTict.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CiMYTict.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var now = CinemaClock.Now;
            // Only include active movies that have at least one upcoming showtime.
            // No tracking keeps this read-only landing-page query lightweight.
            var movies = await _context.Movies
                .AsNoTracking()
                .Where(m => m.IsActive && (!m.TheaterEndDate.HasValue || m.TheaterEndDate.Value >= CinemaClock.Today) && m.Showtimes.Any(s => s.StartTime >= now))
                .OrderByDescending(m => m.ReleaseDate)
                .ThenBy(m => m.Title)
                .ToListAsync();

            return View(movies);
        }

        public async Task<IActionResult> MovieDetails(int id)
        {
            var now = CinemaClock.Now;
            var movie = await _context.Movies
                .Include(m => m.Showtimes.Where(s => s.StartTime >= now))
                .ThenInclude(s => s.CinemaHall)
                .FirstOrDefaultAsync(m => m.MovieId == id && m.IsActive && (!m.TheaterEndDate.HasValue || m.TheaterEndDate.Value >= CinemaClock.Today));

            if (movie == null) return NotFound();
            return View(movie);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new CiMYTict.ViewModels.ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}