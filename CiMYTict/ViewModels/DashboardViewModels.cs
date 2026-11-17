using CiMYTict.Models;

namespace CiMYTict.ViewModels
{
    public class AdminDashboardViewModel
    {
        public decimal DailyRevenue { get; set; }
        public decimal WeeklyRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalTicketsSold { get; set; }
        public int ActiveMoviesCount { get; set; }

        public List<MovieSalesItem> MovieSales { get; set; } = new();
        public List<ShowtimeOccupancyItem> ShowtimeOccupancies { get; set; } = new();
        public List<Booking> RecentBookings { get; set; } = new();
    }

    public class MovieSalesItem
    {
        public string MovieTitle { get; set; } = string.Empty;
        public int TicketsSold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class ShowtimeOccupancyItem
    {
        public string MovieTitle { get; set; } = string.Empty;
        public string HallName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public int SoldSeats { get; set; }
        public int TotalCapacity { get; set; }
        public double OccupancyPercentage => TotalCapacity > 0
            ? Math.Round((double)SoldSeats / TotalCapacity * 100, 1)
            : 0;
    }
}