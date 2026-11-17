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
                showtime.EndTime = showtime.StartTime.AddMinutes(movie.DurationMinutes + 20);
                bool overlap = await _context.Showtimes.AnyAsync(s =>
                    s.CinemaHallId == showtime.CinemaHallId &&
                    showtime.StartTime < s.EndTime &&
                    showtime.EndTime > s.StartTime);

                if (overlap)
                    ModelState.AddModelError("", "Schedule conflict: Another showtime is already booked in this hall during that time.");
            }

            if (!ModelState.IsValid)
            {
                await LoadListsAsync(showtime.MovieId, showtime.CinemaHallId);
                return View(showtime);
            }

            _context.Showtimes.Add(showtime);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Showtime scheduled successfully!";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var showtime = await _context.Showtimes
                .Include(s => s.Bookings)
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);
            if (showtime == null) return NotFound();

            await LoadListsAsync(showtime.MovieId, showtime.CinemaHallId);
            ViewBag.HasBookings = showtime.Bookings.Any();
            return View(showtime);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Showtime showtime)
        {
            if (id != showtime.ShowtimeId) return NotFound();

            var existing = await _context.Showtimes
                .Include(s => s.Bookings)
                .FirstOrDefaultAsync(s => s.ShowtimeId == id);
            if (existing == null) return NotFound();

            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.MovieId == showtime.MovieId && m.IsActive);
            if (movie == null) ModelState.AddModelError(nameof(Showtime.MovieId), "Please choose an active movie.");

            if (!await _context.CinemaHalls.AnyAsync(h => h.CinemaHallId == showtime.CinemaHallId))
                ModelState.AddModelError(nameof(Showtime.CinemaHallId), "Please choose a valid hall.");

            if (showtime.StartTime <= CinemaClock.Now)
                ModelState.AddModelError(nameof(Showtime.StartTime), "Start time must be in the future.");

            if (movie != null)
            {
                showtime.EndTime = showtime.StartTime.AddMinutes(movie.DurationMinutes + 20);

                bool scheduleChanged = existing.MovieId != showtime.MovieId ||
                    existing.CinemaHallId != showtime.CinemaHallId ||
                    existing.StartTime != showtime.StartTime;

                if (existing.Bookings.Any() && scheduleChanged)
                    ModelState.AddModelError("", "This showtime already has bookings. Its movie, hall and start time cannot be changed so existing tickets remain valid.");

                if (ModelState.IsValid)
                {
                    bool overlap = await _context.Showtimes.AnyAsync(s =>
                        s.ShowtimeId != id &&
                        s.CinemaHallId == showtime.CinemaHallId &&
                        showtime.StartTime < s.EndTime &&
                        showtime.EndTime > s.StartTime);

                    if (overlap)
                        ModelState.AddModelError("", "Schedule conflict: Another showtime is already booked in this hall during that time.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadListsAsync(showtime.MovieId, showtime.CinemaHallId);
                ViewBag.HasBookings = existing.Bookings.Any();
                return View(showtime);
            }

            existing.MovieId = showtime.MovieId;
            existing.CinemaHallId = showtime.CinemaHallId;
            existing.StartTime = showtime.StartTime;
            existing.EndTime = showtime.EndTime;
            existing.BaseTicketPrice = showtime.BaseTicketPrice;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Showtime updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadListsAsync(int? selectedMovieId = null, int? selectedHallId = null)
        {
            ViewBag.Movies = new SelectList(
                await _context.Movies.Where(m => m.IsActive).OrderBy(m => m.Title).ToListAsync(),
                "MovieId", "Title", selectedMovieId);
            ViewBag.Halls = new SelectList(
                await _context.CinemaHalls.OrderBy(h => h.HallName).ToListAsync(),
                "CinemaHallId", "HallName", selectedHallId);
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
