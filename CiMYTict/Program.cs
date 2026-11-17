using CiMYTict.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add MVC
builder.Services.AddControllersWithViews();

// Register EF Core with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Cookie Authentication for Module 1 (Role-Based Access Control)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(4);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

var app = builder.Build();

// Run Database Seeder on startup (demo accounts are only created in Development)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    DbInitializer.Initialize(context, app.Environment.IsDevelopment());

    // EnsureCreated() only creates a new database; it does not add columns to an
    // existing database. Keep this small, idempotent schema patch here so older
    // CiMYTict databases receive the cancellation-request columns on startup.
    context.Database.ExecuteSqlRaw("""
        IF OBJECT_ID(N'dbo.Bookings', N'U') IS NULL
            THROW 51000, 'CiMYTict database is missing dbo.Bookings. Check DefaultConnection.', 1;

        IF COL_LENGTH(N'dbo.Bookings', N'CancellationReason') IS NULL
            ALTER TABLE dbo.Bookings ADD CancellationReason NVARCHAR(500) NULL;

        IF COL_LENGTH(N'dbo.Bookings', N'CancellationRequestedAt') IS NULL
            ALTER TABLE dbo.Bookings ADD CancellationRequestedAt DATETIME2 NULL;

        IF COL_LENGTH(N'dbo.Bookings', N'CancellationDecision') IS NULL
            ALTER TABLE dbo.Bookings ADD CancellationDecision NVARCHAR(20) NULL;
        """);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // Must come before UseAuthorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();