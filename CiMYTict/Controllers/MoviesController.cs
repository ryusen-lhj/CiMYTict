using CiMYTict.Data;
using CiMYTict.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CiMYTict.Controllers
{
    [Authorize(Roles = "Staff")]
    public class MoviesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public MoviesController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _context.Movies.OrderByDescending(m => m.ReleaseDate).ToListAsync());
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Movie movie, IFormFile? posterFile)
        {
            ValidateTrailer(movie);
            ValidatePoster(posterFile);
            if (!ModelState.IsValid) return View(movie);

            if (posterFile != null && posterFile.Length > 0)
            {
                movie.PosterUrl = await SavePosterAsync(posterFile);
            }

            _context.Movies.Add(movie);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Movie '{movie.Title}' added!";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie == null) return NotFound();
            return View(movie);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Movie movie, IFormFile? posterFile)
        {
            if (id != movie.MovieId) return NotFound();
            ValidateTrailer(movie);
            ValidatePoster(posterFile);
            if (!ModelState.IsValid) return View(movie);

            var existing = await _context.Movies.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Title = movie.Title;
            existing.Genre = movie.Genre;
            existing.Description = movie.Description;
            existing.DurationMinutes = movie.DurationMinutes;
            existing.AgeRating = movie.AgeRating;
            existing.ReleaseDate = movie.ReleaseDate;
            existing.TrailerUrl = movie.TrailerUrl;
            existing.IsActive = movie.IsActive;

            if (posterFile != null && posterFile.Length > 0)
            {
                existing.PosterUrl = await SavePosterAsync(posterFile);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Movie updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var movie = await _context.Movies.FindAsync(id);
            if (movie != null)
            {
                movie.IsActive = false; // Soft delete so historical tickets remain intact
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Movie '{movie.Title}' archived.";
            }
            return RedirectToAction(nameof(Index));
        }

        private static readonly string[] AllowedPosterExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxPosterBytes = 5 * 1024 * 1024;

        private void ValidatePoster(IFormFile? file)
        {
            if (file == null || file.Length == 0) return;

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedPosterExtensions.Contains(ext) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError("posterFile", "Poster must be a JPG, PNG or WEBP image.");
            else if (file.Length > MaxPosterBytes)
                ModelState.AddModelError("posterFile", "Poster must be 5 MB or smaller.");
        }

        // Only YouTube embed URLs may be rendered inside the trailer iframe
        private void ValidateTrailer(Movie movie)
        {
            if (string.IsNullOrWhiteSpace(movie.TrailerUrl)) { movie.TrailerUrl = null; return; }

            if (!Uri.TryCreate(movie.TrailerUrl.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !(uri.Host is "www.youtube.com" or "www.youtube-nocookie.com") ||
                !uri.AbsolutePath.StartsWith("/embed/", StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(Movie.TrailerUrl), "Trailer must be a YouTube embed URL (https://www.youtube.com/embed/...).");
            }
        }

        private async Task<string> SavePosterAsync(IFormFile file)
        {
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "posters");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/posters/{fileName}";
        }
    }
}