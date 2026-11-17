using CiMYTict.Models;

namespace CiMYTict.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context, bool seedDemoData)
        {
            context.Database.EnsureCreated();

            // Demo accounts, movies and showtimes have publicly known credentials/content: never seed them in production
            if (!seedDemoData) return;

            // 1. Seed Users (Staff & Customer)
            if (!context.Users.Any())
            {
                context.Users.AddRange(
                    new User
                    {
                        FullName = "CiMYTict Admin",
                        Email = "admin@cimytict.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                        PhoneNumber = "012-3456789",
                        Role = UserRole.Staff,
                        RewardPoints = 0
                    },
                    new User
                    {
                        FullName = "Ahmad Daniel",
                        Email = "customer@cimytict.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Cust@123"),
                        PhoneNumber = "019-8765432",
                        Role = UserRole.Customer,
                        RewardPoints = 500
                    }
                );
                context.SaveChanges();
            }

            // 2. Seed Cinema Halls & Seats
            if (!context.CinemaHalls.Any())
            {
                var hall1 = new CinemaHall { HallName = "Hall 1 - Standard & VIP", TotalRows = 6, SeatsPerRow = 10 };
                var hall2 = new CinemaHall { HallName = "Hall 2 - IMAX Experience", TotalRows = 8, SeatsPerRow = 12 };

                context.CinemaHalls.AddRange(hall1, hall2);
                context.SaveChanges();

                GenerateSeatsForHall(context, hall1);
                GenerateSeatsForHall(context, hall2);
                context.SaveChanges();
            }

            // 3. Seed Movies
            if (!context.Movies.Any())
            {
                context.Movies.AddRange(
                    new Movie
                    {
                        Title = "Eclipse of Nebula",
                        Genre = "Sci-Fi / Action",
                        Description = "A rogue space crew races across a collapsing star system to recover a lost quantum beacon.",
                        DurationMinutes = 138,
                        AgeRating = "P13",
                        ReleaseDate = DateTime.Today.AddDays(-5),
                        PosterUrl = "https://placehold.co/400x600/1a1a2e/e94560?text=Eclipse+of+Nebula",
                        TrailerUrl = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                        IsActive = true
                    },
                    new Movie
                    {
                        Title = "Midnight in Kuala Lumpur",
                        Genre = "Thriller / Mystery",
                        Description = "A detective uncovers a high-stakes cyber conspiracy stretching across Bukit Bintang after midnight.",
                        DurationMinutes = 115,
                        AgeRating = "18",
                        ReleaseDate = DateTime.Today.AddDays(-2),
                        PosterUrl = "https://placehold.co/400x600/0f3460/ffffff?text=Midnight+in+KL",
                        TrailerUrl = "https://www.youtube.com/embed/dQw4w9WgXcQ",
                        IsActive = true
                    }
                );
                context.SaveChanges();
            }

            // 4. Seed Upcoming Showtimes
            if (!context.Showtimes.Any())
            {
                var movie1 = context.Movies.First();
                var movie2 = context.Movies.Skip(1).First();
                var hall1 = context.CinemaHalls.First();
                var hall2 = context.CinemaHalls.Skip(1).First();

                var tomorrow = DateTime.Today.AddDays(1);

                context.Showtimes.AddRange(
                    new Showtime
                    {
                        MovieId = movie1.MovieId,
                        CinemaHallId = hall1.CinemaHallId,
                        StartTime = tomorrow.AddHours(14),
                        EndTime = tomorrow.AddHours(14).AddMinutes(movie1.DurationMinutes + 20),
                        BaseTicketPrice = 18.00m
                    },
                    new Showtime
                    {
                        MovieId = movie1.MovieId,
                        CinemaHallId = hall2.CinemaHallId,
                        StartTime = tomorrow.AddHours(20),
                        EndTime = tomorrow.AddHours(20).AddMinutes(movie1.DurationMinutes + 20),
                        BaseTicketPrice = 24.00m
                    },
                    new Showtime
                    {
                        MovieId = movie2.MovieId,
                        CinemaHallId = hall1.CinemaHallId,
                        StartTime = tomorrow.AddHours(18),
                        EndTime = tomorrow.AddHours(18).AddMinutes(movie2.DurationMinutes + 20),
                        BaseTicketPrice = 16.00m
                    }
                );
                context.SaveChanges();
            }

            // 5. Seed Promotions
            if (!context.Promotions.Any())
            {
                context.Promotions.AddRange(
                    new Promotion
                    {
                        Code = "CIMY20",
                        Description = "20% Off Weekend Special (Max RM15)",
                        DiscountPercentage = 20,
                        MaxDiscountAmount = 15.00m,
                        ValidFrom = DateTime.Today.AddDays(-10),
                        ValidUntil = DateTime.Today.AddMonths(3),
                        IsActive = true
                    },
                    new Promotion
                    {
                        Code = "STUDENT10",
                        Description = "10% Student Discount (Max RM8)",
                        DiscountPercentage = 10,
                        MaxDiscountAmount = 8.00m,
                        ValidFrom = DateTime.Today.AddDays(-10),
                        ValidUntil = DateTime.Today.AddMonths(6),
                        IsActive = true
                    }
                );
                context.SaveChanges();
            }
        }

        private static void GenerateSeatsForHall(ApplicationDbContext context, CinemaHall hall)
        {
            for (int r = 0; r < hall.TotalRows; r++)
            {
                string rowLabel = ((char)('A' + r)).ToString();
                for (int s = 1; s <= hall.SeatsPerRow; s++)
                {
                    SeatCategory category = SeatCategory.Standard;
                    decimal extraPrice = 0.00m;

                    if (r == hall.TotalRows - 1) // Back row = Couple Seats
                    {
                        category = SeatCategory.Couple;
                        extraPrice = 10.00m;
                    }
                    else if (r >= hall.TotalRows - 3) // Middle-back rows = VIP
                    {
                        category = SeatCategory.VIP;
                        extraPrice = 5.00m;
                    }

                    context.Seats.Add(new Seat
                    {
                        CinemaHallId = hall.CinemaHallId,
                        RowLabel = rowLabel,
                        SeatNumber = s,
                        Category = category,
                        ExtraPrice = extraPrice
                    });
                }
            }
        }
    }
}