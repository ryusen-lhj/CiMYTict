using CiMYTict.Data;
using CiMYTict.Models;
using CiMYTict.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CiMYTict.Controllers
{
    [Authorize(Roles = "Staff")]
    public class PromotionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PromotionsController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index() => View(await _context.Promotions.ToListAsync());

        public IActionResult Create() => View(new Promotion { ValidFrom = CinemaClock.Today, ValidUntil = CinemaClock.Today.AddMonths(1) });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Promotion promo)
        {
            if (promo.ValidUntil < promo.ValidFrom)
                ModelState.AddModelError(nameof(Promotion.ValidUntil), "End date must be on or after the start date.");

            var code = promo.Code?.Trim().ToUpper() ?? "";
            if (code.Length > 0 && await _context.Promotions.AnyAsync(p => p.Code == code))
                ModelState.AddModelError(nameof(Promotion.Code), "This promo code already exists.");

            if (!ModelState.IsValid) return View(promo);
            promo.Code = code;
            _context.Promotions.Add(promo);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Promo code '{promo.Code}' created!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var promo = await _context.Promotions.FindAsync(id);
            if (promo != null)
            {
                promo.IsActive = !promo.IsActive;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}