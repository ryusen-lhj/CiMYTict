using CiMYTict.Models;

namespace CiMYTict.ViewModels
{
    public class PaymentViewModel
    {
        public Booking Booking { get; set; } = null!;
        public string PaymentMethod { get; set; } = string.Empty;
        public string? PromoCode { get; set; }
        public bool RedeemPoints { get; set; }
        public decimal PromoDiscount { get; set; }
        public int PointsToRedeem { get; set; }
        public decimal PointsDiscount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}