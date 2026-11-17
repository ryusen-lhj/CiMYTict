namespace CiMYTict.Models
{
    public enum UserRole
    {
        Customer = 0,
        Staff = 1
    }

    public enum SeatCategory
    {
        Standard = 0,
        VIP = 1,
        Couple = 2
    }

    public enum BookingStatus
    {
        Pending = 0,    // Temporarily reserved (10-minute lock)
        Confirmed = 1,  // Paid & E-Ticket issued
        Cancelled = 2,
        Expired = 3
    }

    public enum PaymentStatus
    {
        Pending = 0,
        Completed = 1,
        Failed = 2,
        Refunded = 3
    }
}
