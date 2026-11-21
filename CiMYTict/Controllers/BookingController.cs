using CiMYTict.Data;
using CiMYTict.Models;
using CiMYTict.Services;
using CiMYTict.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Data;
using System.Security.Claims;
using System.Net.Mail;
using System.Net;

namespace CiMYTict.Controllers
{
    [Authorize]
    public class BookingController : Controller
    {
        private const int MaxSeatsPerBooking = 10;
        private static readonly string[] AllowedPaymentMethods =
        {
            "Credit / Debit Card", "Touch 'n Go eWallet", "FPX Online Banking"
        };

        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public BookingController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Helper: Release expired temporary seat locks (Module 5)
        private async Task ReleaseExpiredLocksAsync()
        {
            var now = DateTime.UtcNow;
            var expired = await _context.Bookings
                .Where(b => b.Status == BookingStatus.Pending && b.LockExpiresAt < now)
                .ToListAsync();

            if (expired.Count > 0)
            {
                foreach (var b in expired)
                    b.Status = BookingStatus.Expired;

                await _context.SaveChangesAsync();
            }
        }

        // STEP A: Interactive Seat Selection Map (Module 5)
        [HttpGet]
        public async Task<IActionResult> SelectSeats(int showtimeId)
        {
            await ReleaseExpiredLocksAsync();

            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .Include(s => s.CinemaHall)
                .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId);

            if (showtime == null || showtime.Movie == null || !showtime.Movie.IsActive)
                return NotFound();

            if (showtime.StartTime <= CinemaClock.Now)
            {
                TempData["Error"] = "This showtime has already started.";
                return RedirectToAction("MovieDetails", "Home", new { id = showtime.MovieId });
            }

            var allSeats = await _context.Seats
                .Where(s => s.CinemaHallId == showtime.CinemaHallId)
                .OrderBy(s => s.RowLabel)
                .ThenBy(s => s.SeatNumber)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var occupiedSeatIds = await _context.BookingSeats
                .Where(bs => bs.Booking!.ShowtimeId == showtimeId &&
                             (bs.Booking.Status == BookingStatus.Confirmed ||
                              bs.Booking.Status == BookingStatus.CancellationRequested ||
                              (bs.Booking.Status == BookingStatus.Pending && bs.Booking.LockExpiresAt > now)))
                .Select(bs => bs.SeatId)
                .ToHashSetAsync();

            ViewBag.IsP18 = IsP18Rating(showtime.Movie.AgeRating);
            return View(new SeatMapViewModel
            {
                Showtime = showtime,
                AllSeats = allSeats,
                OccupiedSeatIds = occupiedSeatIds
            });
        }

        // STEP B: Lock Selected Seats for 10 Minutes & Proceed to Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReserveSeats(int showtimeId, List<int> selectedSeatIds, bool adultConfirmed = false)
        {
            var seatIds = (selectedSeatIds ?? new List<int>()).Distinct().ToList();

            if (seatIds.Count == 0)
            {
                TempData["Error"] = "Please select at least one seat.";
                return RedirectToAction(nameof(SelectSeats), new { showtimeId });
            }
            if (seatIds.Count > MaxSeatsPerBooking)
            {
                TempData["Error"] = $"You can book at most {MaxSeatsPerBooking} seats at a time.";
                return RedirectToAction(nameof(SelectSeats), new { showtimeId });
            }

            await ReleaseExpiredLocksAsync();

            var showtime = await _context.Showtimes
                .Include(s => s.Movie)
                .FirstOrDefaultAsync(s => s.ShowtimeId == showtimeId);
            if (showtime == null || showtime.Movie == null || !showtime.Movie.IsActive) return NotFound();

            if (showtime.StartTime <= CinemaClock.Now)
            {
                TempData["Error"] = "This showtime has already started.";
                return RedirectToAction("MovieDetails", "Home", new { id = showtime.MovieId });
            }

            if (IsP18Rating(showtime.Movie.AgeRating) && !adultConfirmed)
            {
                TempData["Error"] = "You must confirm that you are 18 years old or above before booking a P18 movie.";
                return RedirectToAction(nameof(SelectSeats), new { showtimeId });
            }

            // Only seats that belong to THIS showtime's hall are valid
            var seats = await _context.Seats
                .Where(s => seatIds.Contains(s.SeatId) && s.CinemaHallId == showtime.CinemaHallId)
                .ToListAsync();

            if (seats.Count != seatIds.Count)
            {
                TempData["Error"] = "One or more selected seats are invalid for this showtime.";
                return RedirectToAction(nameof(SelectSeats), new { showtimeId });
            }

            int userId = CurrentUserId;
            decimal subTotal = seats.Sum(s => showtime.BaseTicketPrice + s.ExtraPrice);

            // Serializable transaction: the conflict check and the insert can't interleave with another request
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var now = DateTime.UtcNow;
            bool conflict = await _context.BookingSeats.AnyAsync(bs =>
                bs.Booking!.ShowtimeId == showtimeId &&
                seatIds.Contains(bs.SeatId) &&
                (bs.Booking.Status == BookingStatus.Confirmed ||
                 bs.Booking.Status == BookingStatus.CancellationRequested ||
                 (bs.Booking.Status == BookingStatus.Pending && bs.Booking.LockExpiresAt > now)));

            if (conflict)
            {
                await tx.RollbackAsync();
                TempData["Error"] = "One or more selected seats were just taken. Please pick available seats.";
                return RedirectToAction(nameof(SelectSeats), new { showtimeId });
            }

            var booking = new Booking
            {
                BookingReference = $"CMT-{CinemaClock.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                UserId = userId,
                ShowtimeId = showtimeId,
                SubTotal = subTotal,
                DiscountAmount = 0m,
                TotalAmount = subTotal,
                Status = BookingStatus.Pending,
                CreatedAt = now,
                LockExpiresAt = now.AddMinutes(10)
            };

            foreach (var seat in seats)
            {
                booking.BookingSeats.Add(new BookingSeat
                {
                    SeatId = seat.SeatId,
                    SeatPrice = showtime.BaseTicketPrice + seat.ExtraPrice
                });
            }

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return RedirectToAction(nameof(Checkout), new { bookingId = booking.BookingId });
        }

        // STEP C: Checkout & Payment Page (Modules 6 & 7)
        [HttpGet]
        public async Task<IActionResult> Checkout(int bookingId)
        {
            int userId = CurrentUserId;
            var booking = await _context.Bookings
                .Include(b => b.Showtime)!.ThenInclude(s => s!.Movie)
                .Include(b => b.Showtime)!.ThenInclude(s => s!.CinemaHall)
                .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

            if (booking == null || booking.Status != BookingStatus.Pending || booking.LockExpiresAt < DateTime.UtcNow)
            {
                TempData["Error"] = "Your seat reservation has expired. Please select seats again.";
                return RedirectToAction("Index", "Home");
            }

            var user = await _context.Users.FindAsync(userId);

            return View(new CheckoutViewModel
            {
                Booking = booking,
                AvailableUserPoints = user?.RewardPoints ?? 0,
                AvailablePromotions = new List<Promotion>()
            });
        }

        [HttpGet]
        public async Task<IActionResult> Payment(int bookingId, string? promoCode, bool redeemPoints, string paymentMethod)
        {
            var booking = await _context.Bookings
                .Include(b => b.Showtime)!.ThenInclude(s => s!.Movie)
                .Include(b => b.Showtime)!.ThenInclude(s => s!.CinemaHall)
                .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == CurrentUserId);

            if (booking == null || booking.Status != BookingStatus.Pending || booking.LockExpiresAt < DateTime.UtcNow)
            {
                TempData["Error"] = "Your seat reservation has expired. Please select seats again.";
                return RedirectToAction("Index", "Home");
            }

            if (!AllowedPaymentMethods.Contains(paymentMethod))
            {
                TempData["Error"] = "Please choose a valid payment method.";
                return RedirectToAction(nameof(Checkout), new { bookingId });
            }

            decimal promoDiscount = 0m;
            int pointsToRedeem = 0;
            if (!string.IsNullOrWhiteSpace(promoCode))
            {
                var code = promoCode.Trim().ToUpperInvariant();
                var promo = await _context.Promotions.FirstOrDefaultAsync(p =>
                    p.Code == code && p.IsActive &&
                    p.ValidFrom <= CinemaClock.Today && p.ValidUntil >= CinemaClock.Today);
                if (promo == null)
                {
                    TempData["Error"] = $"Promo code '{code}' is invalid or expired.";
                    return RedirectToAction(nameof(Checkout), new { bookingId });
                }
                promoDiscount = Math.Min(booking.SubTotal * promo.DiscountPercentage / 100m, promo.MaxDiscountAmount);
            }

            var user = await _context.Users.FindAsync(CurrentUserId);
            if (user == null) return Unauthorized();
            if (redeemPoints && user.RewardPoints >= 100)
            {
                var remaining = Math.Max(0m, booking.SubTotal - promoDiscount);
                var maxPoints = (int)Math.Min(user.RewardPoints, Math.Floor(remaining) * 100m);
                pointsToRedeem = (maxPoints / 100) * 100;
            }

            var pointsDiscount = pointsToRedeem / 100m;
            return View(new CiMYTict.ViewModels.PaymentViewModel
            {
                Booking = booking,
                PaymentMethod = paymentMethod,
                PromoCode = promoCode,
                RedeemPoints = redeemPoints,
                PromoDiscount = promoDiscount,
                PointsToRedeem = pointsToRedeem,
                PointsDiscount = pointsDiscount,
                TotalAmount = Math.Max(0m, booking.SubTotal - promoDiscount - pointsDiscount)
            });
        }

        // STEP D: Complete Payment, Apply Discounts/Points, Generate QR Ticket
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int bookingId, string? promoCode, bool redeemPoints, string paymentMethod, string? bankName)
        {
            int userId = CurrentUserId;
            var booking = await _context.Bookings
                .Include(b => b.Showtime)!.ThenInclude(s => s!.Movie)
                .Include(b => b.Showtime)!.ThenInclude(s => s!.CinemaHall)
                .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

            if (booking == null || booking.Status != BookingStatus.Pending || booking.LockExpiresAt < DateTime.UtcNow)
            {
                TempData["Error"] = "Reservation expired before payment completed.";
                return RedirectToAction("Index", "Home");
            }

            var allowedBanks = new[] { "Maybank", "CIMB Bank", "Public Bank", "RHB Bank", "Hong Leong Bank", "AmBank", "Alliance Bank", "Affin Bank" };
            if (!AllowedPaymentMethods.Contains(paymentMethod) ||
                (paymentMethod == "FPX Online Banking" && !allowedBanks.Contains(bankName)))
            {
                TempData["Error"] = "Please choose a valid payment method and bank.";
                return RedirectToAction(nameof(Checkout), new { bookingId });
            }

            var recordedPaymentMethod = paymentMethod == "FPX Online Banking"
                ? $"FPX - {bankName}"
                : paymentMethod == "Touch 'n Go eWallet"
                    ? "Touch 'n Go DuitNow QR"
                    : "Card Payment (Demo)";

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Unauthorized();

            decimal discount = 0m;

            // 1. Apply Promo Code if valid (Module 7). An invalid code stops the payment BEFORE anything is charged.
            if (!string.IsNullOrWhiteSpace(promoCode))
            {
                var code = promoCode.Trim().ToUpper();
                var today = CinemaClock.Today;
                var promo = await _context.Promotions.FirstOrDefaultAsync(p =>
                    p.Code == code && p.IsActive &&
                    p.ValidFrom <= today && p.ValidUntil >= today);

                if (promo == null)
                {
                    TempData["Error"] = $"Promo code '{code}' is invalid or expired. Fix or clear it and try again.";
                    return RedirectToAction(nameof(Checkout), new { bookingId });
                }

                var rawDiscount = booking.SubTotal * (promo.DiscountPercentage / 100m);
                discount += Math.Min(rawDiscount, promo.MaxDiscountAmount);
                booking.PromotionId = promo.PromotionId;
            }

            // 2. Redeem Reward Points: 100 pts = RM 1.00 (Module 7)
            int pointsUsed = 0;
            if (redeemPoints && user.RewardPoints >= 100)
            {
                decimal remainingAfterPromo = Math.Max(0, booking.SubTotal - discount);
                int maxRedeemablePoints = (int)Math.Min(user.RewardPoints, Math.Floor(remainingAfterPromo) * 100);
                pointsUsed = (maxRedeemablePoints / 100) * 100;
                discount += pointsUsed / 100m;
                user.RewardPoints -= pointsUsed;
            }

            booking.PointsRedeemed = pointsUsed;
            booking.DiscountAmount = discount;
            booking.TotalAmount = Math.Max(0, booking.SubTotal - discount);
            booking.Status = BookingStatus.Confirmed;
            booking.LockExpiresAt = null;

            // 3. Generate E-Ticket QR Code (Module 6)
            string seatList = string.Join(", ", booking.BookingSeats.Select(bs => bs.Seat!.SeatCode));
            string qrPayload = $"CiMYTict|{booking.BookingReference}|{booking.Showtime!.Movie!.Title}|{booking.Showtime.StartTime:yyyy-MM-dd HH:mm}|Seats:{seatList}";
            booking.QrCodeBase64 = GenerateQrCodeBase64(qrPayload);

            // 4. Record Payment
            _context.Payments.Add(new Payment
            {
                BookingId = booking.BookingId,
                PaymentMethod = recordedPaymentMethod,
                TransactionId = $"TXN-{Guid.NewGuid().ToString("N")[..10].ToUpper()}",
                AmountPaid = booking.TotalAmount,
                Status = PaymentStatus.Completed,
                PaidAt = DateTime.UtcNow
            });

            // 5. Award new loyalty points (10 pts per RM 1 spent) and remember them for cancellation
            booking.PointsEarned = (int)Math.Floor(booking.TotalAmount * 10);
            user.RewardPoints += booking.PointsEarned;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment successful! Your E-Ticket is ready.";
            return RedirectToAction(nameof(Ticket), new { bookingId = booking.BookingId });
        }

        // STEP E: E-Ticket & QR Code View (Module 6)
        [HttpGet]
        public async Task<IActionResult> Ticket(int bookingId)
        {
            int userId = CurrentUserId;
            bool isStaff = User.IsInRole("Staff");
            var booking = await _context.Bookings
                .Include(b => b.Showtime)!.ThenInclude(s => s!.Movie)
                .Include(b => b.Showtime)!.ThenInclude(s => s!.CinemaHall)
                .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && (b.UserId == userId || isStaff));

            if (booking == null || (booking.Status != BookingStatus.Confirmed && booking.Status != BookingStatus.CancellationRequested)) return NotFound();
            return View(booking);
        }

        // STEP F: Customer Booking History & Cancellation (Module 1 & 6)
        [HttpGet]
        public async Task<IActionResult> MyBookings()
        {
            int userId = CurrentUserId;
            var bookings = await _context.Bookings
                .Include(b => b.Showtime)!.ThenInclude(s => s!.Movie)
                .Include(b => b.Showtime)!.ThenInclude(s => s!.CinemaHall)
                .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
                .Include(b => b.Payment)
                .Where(b => b.UserId == userId && b.Status != BookingStatus.Pending)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return View(bookings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int bookingId, string reason)
        {
            int userId = CurrentUserId;
            var booking = await _context.Bookings
                .Include(b => b.Showtime)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

            reason = (reason ?? string.Empty).Trim();
            if (booking != null && booking.Status == BookingStatus.Confirmed &&
                booking.Showtime!.StartTime > CinemaClock.Now.AddHours(2) &&
                !string.IsNullOrWhiteSpace(reason) && reason.Length <= 500)
            {
                booking.Status = BookingStatus.CancellationRequested;
                booking.CancellationReason = reason;
                booking.CancellationRequestedAt = CinemaClock.Now;
                booking.CancellationDecision = null;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Cancellation request for {booking.BookingReference} has been sent to the admin for review.";
            }
            else
            {
                TempData["Error"] = "Please provide a reason (up to 500 characters). Requests are allowed at least 2 hours before showtime.";
            }

            return RedirectToAction(nameof(MyBookings));
        }

        // Staff review queue for customer cancellation requests.
        [Authorize(Roles = "Staff")]
        [HttpGet]
        public async Task<IActionResult> CancellationRequests()
        {
            var requests = await _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Showtime)!.ThenInclude(s => s!.Movie)
                .Include(b => b.Showtime)!.ThenInclude(s => s!.CinemaHall)
                .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
                .Where(b => b.Status == BookingStatus.CancellationRequested)
                .OrderBy(b => b.CancellationRequestedAt)
                .ToListAsync();
            return View(requests);
        }

        [Authorize(Roles = "Staff")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewCancellation(int bookingId, string decision)
        {
            var booking = await _context.Bookings
                .Include(b => b.Payment)
                .Include(b => b.Showtime)!.ThenInclude(s => s!.Movie)
                .Include(b => b.Showtime)!.ThenInclude(s => s!.CinemaHall)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.Status == BookingStatus.CancellationRequested);
            if (booking == null) return NotFound();

            if (decision == "Approve")
            {
                booking.Status = BookingStatus.Cancelled;
                booking.CancellationDecision = "Approved";
                if (booking.Payment != null) booking.Payment.Status = PaymentStatus.Refunded;

                var user = await _context.Users.FindAsync(booking.UserId);
                if (user != null)
                {
                    user.RewardPoints += booking.PointsRedeemed;
                    user.RewardPoints = Math.Max(0, user.RewardPoints - booking.PointsEarned);
                }
                TempData["Success"] = $"Cancellation approved for {booking.BookingReference}. Payment marked as refunded.";
            }
            else if (decision == "Reject")
            {
                booking.Status = BookingStatus.Confirmed;
                booking.CancellationDecision = "Rejected";
                TempData["Success"] = $"Cancellation rejected for {booking.BookingReference}. The ticket remains confirmed.";
            }
            else
            {
                TempData["Error"] = "Invalid cancellation decision.";
                return RedirectToAction(nameof(CancellationRequests));
            }

            await _context.SaveChangesAsync();

            // Email the customer about the decision; admin receives an on-page confirmation via TempData.
            var customer = await _context.Users.FindAsync(booking.UserId);
            if (customer != null)
            {
                try { await SendCancellationDecisionEmailAsync(customer.Email, booking, decision); }
                catch (Exception ex)
                {
                    TempData["Error"] = (TempData["Error"] as string ?? "") + " Customer email notification could not be sent; check SMTP settings.";
                }
            }
            return RedirectToAction(nameof(CancellationRequests));
        }

                private async Task SendCancellationDecisionEmailAsync(string recipient, Booking booking, string decision)
        {
            var host = _configuration["Email:SmtpHost"];
            var username = _configuration["Email:SmtpUsername"];
            var password = _configuration["Email:SmtpPassword"];
            var from = _configuration["Email:From"];
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
                throw new InvalidOperationException("SMTP email settings are not configured.");

            var approved = decision == "Approve";
            var movie = booking.Showtime?.Movie?.Title ?? "your movie";
            var hall = booking.Showtime?.CinemaHall?.HallName ?? "cinema hall";
            var time = booking.Showtime?.StartTime.ToString("ddd, dd MMM yyyy, hh:mm tt") ?? "scheduled time";
            var subject = approved ? $"Cancellation approved — {booking.BookingReference}" : $"Cancellation rejected — {booking.BookingReference}";
            var body = approved
                ? $"Hello,\n\nYour ticket cancellation request has been APPROVED.\n\nBooking reference: {booking.BookingReference}\nMovie: {movie}\nShowtime: {time}\nHall: {hall}\n\nThe booking is cancelled and the payment has been marked refunded in CiMYTict. Please note this demo system does not issue a real bank/e-wallet refund.\n\nCiMYTict"
                : $"Hello,\n\nYour ticket cancellation request has been REJECTED. Your ticket remains confirmed and active.\n\nBooking reference: {booking.BookingReference}\nMovie: {movie}\nShowtime: {time}\nHall: {hall}\n\nCiMYTict";
            using var message = new MailMessage(from, recipient) { Subject = subject, Body = body, IsBodyHtml = false };
            using var smtp = new SmtpClient(host, int.TryParse(_configuration["Email:SmtpPort"], out var port) ? port : 587)
            {
                EnableSsl = !string.Equals(_configuration["Email:UseSsl"], "false", StringComparison.OrdinalIgnoreCase),
                Credentials = string.IsNullOrWhiteSpace(username) ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(username, password)
            };
            await smtp.SendMailAsync(message);
        }

private static bool IsP18Rating(string? rating) =>
            string.Equals(rating?.Trim(), "P18", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rating?.Trim(), "18", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(rating?.Trim(), "18+", StringComparison.OrdinalIgnoreCase);

        private static string GenerateQrCodeBase64(string content)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            using var pngQr = new PngByteQRCode(qrData);
            byte[] qrBytes = pngQr.GetGraphic(20);
            return Convert.ToBase64String(qrBytes);
        }
    }
}
