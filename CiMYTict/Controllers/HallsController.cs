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
            var halls = await _context.CinemaHalls.Include(h => h.Seats).ToListAsync();
            return View(halls);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CinemaHall hall, int vipRows = 2, bool hasCoupleBackRow = true)
        {
            if (vipRows < 0 || vipRows >= Math.Max(hall.TotalRows, 1))
                ModelState.AddModelError("", "VIP rows must be between 0 and (total rows - 1).");

            if (!ModelState.IsValid) return View(hall);

            _context.CinemaHalls.Add(hall);
            await _context.SaveChangesAsync();

            for (int r = 0; r < hall.TotalRows; r++)
            {
                string rowLabel = ((char)('A' + r)).ToString();
                for (int s = 1; s <= hall.SeatsPerRow; s++)
                {
                    var cat = SeatCategory.Standard;
                    decimal extra = 0m;

                    if (hasCoupleBackRow && r == hall.TotalRows - 1)
                    {
                        cat = SeatCategory.Couple;
                        extra = 10.00m;
                    }
                    else if (r >= hall.TotalRows - (hasCoupleBackRow ? vipRows + 1 : vipRows))
                    {
                        cat = SeatCategory.VIP;
                        extra = 5.00m;
                    }

                    _context.Seats.Add(new Seat
                    {
                        CinemaHallId = hall.CinemaHallId,
                        RowLabel = rowLabel,
                        SeatNumber = s,
                        Category = cat,
                        ExtraPrice = extra
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Hall '{hall.HallName}' created with {hall.Capacity} seats!";
            return RedirectToAction(nameof(Index));
        }
    }
}