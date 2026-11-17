using CiMYTict.Models;

namespace CiMYTict.ViewModels
{
    public class SeatMapViewModel
    {
        public Showtime Showtime { get; set; } = null!;
        public List<Seat> AllSeats { get; set; } = new();
        public HashSet<int> OccupiedSeatIds { get; set; } = new(); // Confirmed OR actively locked
    }

    public class CheckoutViewModel
    {
        public Booking Booking { get; set; } = null!;
        public int AvailableUserPoints { get; set; }
        public List<Promotion> AvailablePromotions { get; set; } = new();
        public string? PromoCode { get; set; }
        public bool RedeemPoints { get; set; }
        public string PaymentMethod { get; set; } = "Credit / Debit Card";
    }
}