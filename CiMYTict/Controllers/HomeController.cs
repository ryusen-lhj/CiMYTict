using System.Diagnostics;
using CiMYTict.Services;
using CiMYTict.ViewModels;
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

        public async Task<IActionResult> Index(string? search, string? genre)
        {
            var now = CinemaClock.Now;
            var query = _context.Movies
                .Where(m => m.IsActive)
                .Include(m => m.Showtimes.Where(s => s.StartTime >= now))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(m => m.Title.Contains(search));

            if (!string.IsNullOrWhiteSpace(genre))
                query = query.Where(m => m.Genre.Contains(genre));

            ViewBag.Search = search;
            ViewBag.Genre = genre;
            ViewBag.Genres = await _context.Movies
                .Where(m => m.IsActive)
                .Select(m => m.Genre)
                .Distinct()
                .ToListAsync();

            return View(await query.OrderByDescending(m => m.ReleaseDate).ToListAsync());
        }

        public async Task<IActionResult> MovieDetails(int id)
        {
            var now = CinemaClock.Now;
            var movie = await _context.Movies
                .Include(m => m.Showtimes.Where(s => s.StartTime >= now))
                .ThenInclude(s => s.CinemaHall)
                .FirstOrDefaultAsync(m => m.MovieId == id && m.IsActive);

            if (movie == null) return NotFound();
            return View(movie);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
