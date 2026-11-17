using CiMYTict.Data;
using CiMYTict.Models;
using CiMYTict.Services;
using CiMYTict.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CiMYTict.Controllers
{
    [Authorize(Roles = "Staff")]
    public class AdminDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // "Today"/"this week"/"this month" are measured in cinema-local time; CreatedAt is stored in UTC.
            var todayLocal = CinemaClock.Today;
            var offset = CinemaClock.Offset;
            DateTime ToUtc(DateTime local) => local - offset;

            var todayStartUtc = ToUtc(todayLocal);
            var weekStartUtc = ToUtc(todayLocal.AddDays(-6));   // today + previous 6 days
            var monthStartUtc = ToUtc(new DateTime(todayLocal.Year, todayLocal.Month, 1));

            // Aggregate in SQL instead of loading every booking into memory
            var confirmed = _context.Bookings.Where(b => b.Status == BookingStatus.Confirmed);

            var movieSales = await confirmed
                .GroupBy(b => b.Showtime!.Movie!.Title)
                .Select(g => new MovieSalesItem
                {
                    MovieTitle = g.Key,
                    TicketsSold = g.Sum(b => b.BookingSeats.Count),
                    Revenue = g.Sum(b => b.TotalAmount)
                })
                .OrderByDescending(m => m.Revenue)
                .ToListAsync();

            var dailyRevenue = await confirmed.Where(b => b.CreatedAt >= todayStartUtc).SumAsync(b => (decimal?)b.TotalAmount) ?? 0m;
            var weeklyRevenue = await confirmed.Where(b => b.CreatedAt >= weekStartUtc).SumAsync(b => (decimal?)b.TotalAmount) ?? 0m;
            var monthlyRevenue = await confirmed.Where(b => b.CreatedAt >= monthStartUtc).SumAsync(b => (decimal?)b.TotalAmount) ?? 0m;
            var totalRevenue = await confirmed.SumAsync(b => (decimal?)b.TotalAmount) ?? 0m;
            var totalTickets = await confirmed.SumAsync(b => (int?)b.BookingSeats.Count) ?? 0;

            var recentBookings = await confirmed
                .Include(b => b.User)
                .Include(b => b.Showtime)!.ThenInclude(s => s!.Movie)
                .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
                .OrderByDescending(b => b.CreatedAt)
                .Take(10)
                .ToListAsync();

            var showtimes = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.CinemaHall)
                .Include(s => s.Bookings.Where(b => b.Status == BookingStatus.Confirmed))
                    .ThenInclude(b => b.BookingSeats)
                .OrderByDescending(s => s.StartTime)
                .Take(10)
                .ToListAsync();

            var occupancies = showtimes.Select(s => new ShowtimeOccupancyItem
            {
                MovieTitle = s.Movie?.Title ?? "Unknown",
                HallName = s.CinemaHall?.HallName ?? "Hall",
                StartTime = s.StartTime,
                SoldSeats = s.Bookings.Sum(b => b.BookingSeats.Count),
                TotalCapacity = s.CinemaHall?.Capacity ?? 0
            }).ToList();

            var vm = new AdminDashboardViewModel
            {
                DailyRevenue = dailyRevenue,
                WeeklyRevenue = weeklyRevenue,
                MonthlyRevenue = monthlyRevenue,
                TotalRevenue = totalRevenue,
                TotalTicketsSold = totalTickets,
                ActiveMoviesCount = await _context.Movies.CountAsync(m => m.IsActive),
                MovieSales = movieSales,
                ShowtimeOccupancies = occupancies,
                RecentBookings = recentBookings
            };

            return View(vm);
        }
        [HttpGet]
        public async Task<IActionResult> ExportCsv()
        {
            var bookings = await _context.Bookings
                .Where(b => b.Status == BookingStatus.Confirmed)
                .Include(b => b.User)
                .Include(b => b.Showtime)!.ThenInclude(s => s!.Movie)
                .Include(b => b.Showtime)!.ThenInclude(s => s!.CinemaHall)
                .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
                .Include(b => b.Payment)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            static string Csv(string? value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
            var csv = new StringBuilder();
            csv.AppendLine("Booking Reference,Customer,Email,Movie,Cinema Hall,Showtime,Seats,Subtotal (RM),Discount (RM),Amount Paid (RM),Payment Method,Transaction ID,Payment Date");
            foreach (var booking in bookings)
            {
                var seats = string.Join(" | ", booking.BookingSeats.Select(x => x.Seat?.SeatCode));
                var fields = new[]
                {
                    Csv(booking.BookingReference),
                    Csv(booking.User?.FullName),
                    Csv(booking.User?.Email),
                    Csv(booking.Showtime?.Movie?.Title),
                    Csv(booking.Showtime?.CinemaHall?.HallName),
                    Csv(booking.Showtime?.StartTime.ToString("yyyy-MM-dd HH:mm")),
                    Csv(seats),
                    booking.SubTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    booking.DiscountAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    booking.TotalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                    Csv(booking.Payment?.PaymentMethod),
                    Csv(booking.Payment?.TransactionId),
                    Csv(booking.Payment?.PaidAt.ToString("yyyy-MM-dd HH:mm"))
                };
                csv.AppendLine(string.Join(",", fields));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", $"CiMYTict-Confirmed-Bookings-{CinemaClock.Today:yyyyMMdd}.csv");
        }

    }
}