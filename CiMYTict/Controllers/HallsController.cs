using CiMYTict.Data;
using CiMYTict.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CiMYTict.Controllers
{
    [Authorize(Roles = "Staff")]
    public class HallsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HallsController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var halls = await _context.CinemaHalls.Include(h => h.Seats).OrderBy(h => h.HallName).ToListAsync();
            return View(halls);
        }

        public IActionResult Create() => View(new CinemaHall());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CinemaHall hall)
        {
            ValidateCapacity(hall.HallType, hall.Capacity);
            if (!ModelState.IsValid) return View(hall);

            _context.CinemaHalls.Add(hall);
            await _context.SaveChangesAsync();

            for (int r = 0; r < hall.TotalRows; r++)
            {
                string rowLabel = ((char)('A' + r)).ToString();
                for (int s = 1; s <= hall.SeatsPerRow; s++)
                {
                    _context.Seats.Add(new Seat
                    {
                        CinemaHallId = hall.CinemaHallId,
                        RowLabel = rowLabel,
                        SeatNumber = s,
                        Category = SeatCategory.Standard,
                        ExtraPrice = 0m
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Hall '{hall.HallName}' created with {hall.Capacity} standard seats. Use Edit Hall to assign seat categories.";
            return RedirectToAction(nameof(Edit), new { id = hall.CinemaHallId });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var hall = await _context.CinemaHalls.Include(h => h.Seats)
                .FirstOrDefaultAsync(h => h.CinemaHallId == id);
            if (hall == null) return NotFound();

            ViewBag.HasBookings = await _context.Bookings.AnyAsync(b => b.Showtime != null && b.Showtime.CinemaHallId == id);
            return View(hall);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            string hallName,
            CinemaHallType hallType,
            int totalRows,
            int seatsPerRow,
            int[] seatIds,
            SeatCategory[] seatCategories,
            decimal[] extraPrices)
        {
            var hall = await _context.CinemaHalls.Include(h => h.Seats)
                .FirstOrDefaultAsync(h => h.CinemaHallId == id);
            if (hall == null) return NotFound();

            hallName = (hallName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(hallName) || hallName.Length > 50)
                ModelState.AddModelError(nameof(hallName), "Hall name is required and must be 50 characters or fewer.");

            if (!Enum.IsDefined(typeof(CinemaHallType), hallType))
                ModelState.AddModelError(nameof(hallType), "Select a valid auditorium type.");

            if (totalRows < 1 || totalRows > 26 || seatsPerRow < 1 || seatsPerRow > 30)
                ModelState.AddModelError(string.Empty, "Rows must be 1-26 and seats per row must be 1-30.");

            int requestedCapacity = totalRows * seatsPerRow;
            ValidateCapacity(hallType, requestedCapacity);

            bool layoutChanged = totalRows != hall.TotalRows || seatsPerRow != hall.SeatsPerRow;
            bool hasBookings = await _context.Bookings.AnyAsync(b => b.Showtime != null && b.Showtime.CinemaHallId == id);

            if (hasBookings && (layoutChanged || hall.HallType != hallType ||
                hall.Seats.Any(s => !seatIds.Contains(s.SeatId))))
            {
                ModelState.AddModelError(string.Empty,
                    "This hall already has bookings. Its capacity, auditorium type and seat categories cannot be changed because existing tickets must remain valid.");
            }

            if (seatIds.Length != hall.Seats.Count || seatCategories.Length != hall.Seats.Count ||
                extraPrices.Length != hall.Seats.Count ||
                !hall.Seats.Select(s => s.SeatId).OrderBy(x => x).SequenceEqual(seatIds.OrderBy(x => x)))
            {
                ModelState.AddModelError(string.Empty, "The submitted seat layout is incomplete or outdated. Reload the page and try again.");
            }

            if (seatCategories.Any(c => !Enum.IsDefined(typeof(SeatCategory), c)))
                ModelState.AddModelError(string.Empty, "One or more seat categories are invalid.");
            if (extraPrices.Any(p => p < 0m || p > 500m))
                ModelState.AddModelError(string.Empty, "Seat surcharges must be between RM0 and RM500.");

            if (!ModelState.IsValid)
            {
                ViewBag.HasBookings = hasBookings;
                return View(hall);
            }

            hall.HallName = hallName;
            hall.HallType = hallType;

            if (layoutChanged)
            {
                _context.Seats.RemoveRange(hall.Seats);
                hall.TotalRows = totalRows;
                hall.SeatsPerRow = seatsPerRow;
                await _context.SaveChangesAsync();

                // A resized layout has a different number of seats, so the previous
                // per-seat selections cannot safely map to the new seat coordinates.
                // Recreate the new layout as Standard; the admin can then assign
                // VIP/Couple categories on the next edit.
                var replacementSeats = new List<Seat>();
                for (int r = 0; r < totalRows; r++)
                {
                    string rowLabel = ((char)('A' + r)).ToString();
                    for (int s = 1; s <= seatsPerRow; s++)
                    {
                        replacementSeats.Add(new Seat
                        {
                            CinemaHallId = hall.CinemaHallId,
                            RowLabel = rowLabel,
                            SeatNumber = s,
                            Category = SeatCategory.Standard,
                            ExtraPrice = 0m
                        });
                    }
                }
                _context.Seats.AddRange(replacementSeats);
            }
            else
            {
                var seatsById = hall.Seats.ToDictionary(s => s.SeatId);
                for (int i = 0; i < seatIds.Length; i++)
                {
                    seatsById[seatIds[i]].Category = seatCategories[i];
                    seatsById[seatIds[i]].ExtraPrice = extraPrices[i];
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Hall '{hall.HallName}' updated successfully ({requestedCapacity} seats).";
            return RedirectToAction(nameof(Index));
        }

        private void ValidateCapacity(CinemaHallType type, int capacity)
        {
            (int min, int max, string label) = type switch
            {
                CinemaHallType.StandardMidSize => (120, 250, "Standard / Mid-size"),
                CinemaHallType.LargeFormatIMAX => (250, 500, "Large format / IMAX"),
                CinemaHallType.SmallIndependent => (50, 120, "Small / Independent"),
                CinemaHallType.VIPRecliner => (40, 80, "VIP / Recliner"),
                CinemaHallType.CouplesThemed => (20, 50, "Couples / Themed suites"),
                _ => (120, 250, "Standard / Mid-size")
            };

            if (capacity < min || capacity > max)
                ModelState.AddModelError(string.Empty,
                    $"{label} auditoriums must have between {min} and {max} seats. Current capacity: {capacity}.");
        }
    }
}