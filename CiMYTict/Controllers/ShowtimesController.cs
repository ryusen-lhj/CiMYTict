using CiMYTict.Data;
using CiMYTict.Models;
using CiMYTict.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CiMYTict.Controllers
{
    [Authorize(Roles = "Staff")]
    public class ShowtimesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShowtimesController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var list = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.CinemaHall)
                .OrderByDescending(s => s.StartTime)
                .ToListAsync();
            return View(list);
        }

        public async Task<IActionResult> Create()
        {
            await LoadListsAsync();
            return View(new Showtime { StartTime = CinemaClock.Now.Date.AddDays(1).AddHours(18), BaseTicketPrice = 18.00m });
        }

        private async Task LoadListsAsync()
        {
            ViewBag.Movies = new SelectList(await _context.Movies.Where(m => m.IsActive).ToListAsync(), "MovieId", "Title");
            ViewBag.Halls = new SelectList(await _context.CinemaHalls.ToListAsync(), "CinemaHallId", "HallName");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Showtime showtime)
        {
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MovieId == showtime.MovieId && m.IsActive);
            if (movie == null) ModelState.AddModelError(nameof(Showtime.MovieId), "Please choose an active movie.");

            if (!await _context.CinemaHalls.AnyAsync(h => h.CinemaHallId == showtime.CinemaHallId))
                ModelState.AddModelError(nameof(Showtime.CinemaHallId), "Please choose a valid hall.");

            if (showtime.StartTime <= CinemaClock.Now)
                ModelState.AddModelError(nameof(Showtime.StartTime), "Start time must be in the future.");

            if (movie != null && ModelState.IsValid)
            {
                // Include 20 mins cleaning buffer
                showtime.EndTime = showtime.StartTime.AddMinutes(movie.DurationMinutes + 20);

                // Prevent overlapping schedules in the same hall
                bool overlap = await _context.Showtimes.AnyAsync(s =>
                    s.CinemaHallId == showtime.CinemaHallId &&
                    showtime.StartTime < s.EndTime &&
                    showtime.EndTime > s.StartTime);

                if (overlap)
                    ModelState.AddModelError("", "Schedule conflict: Another showtime is already booked in this hall during that time.");
            }

            if (!ModelState.IsValid)
            {
                await LoadListsAsync();
                return View(showtime);
            }

            _context.Showtimes.Add(showtime);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Showtime scheduled successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var st = await _context.Showtimes.Include(s => s.Bookings).FirstOrDefaultAsync(s => s.ShowtimeId == id);
            if (st != null && !st.Bookings.Any())
            {
                _context.Showtimes.Remove(st);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Showtime deleted.";
            }
            else
            {
                TempData["Error"] = "Cannot delete showtime with existing bookings.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}