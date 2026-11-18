using CiMYTict.Data;
using CiMYTict.Models;
using CiMYTict.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using System.Security.Claims;

namespace CiMYTict.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AccountController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var email = model.Email.Trim().ToLowerInvariant();
            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                ModelState.AddModelError("Email", "Email is already registered.");
                return View(model);
            }

            var user = new User
            {
                FullName = model.FullName,
                Email = email,
                PhoneNumber = model.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Role = UserRole.Customer,
                RewardPoints = 100 // Welcome bonus points
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            await SignInUserAsync(user);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var email = model.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            var now = DateTime.UtcNow;
            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > now)
            {
                var remaining = user.LockoutEnd.Value - now;
                ModelState.AddModelError(string.Empty,
                    $"Too many failed attempts. Please try again in {Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))} seconds.");
                return View(model);
            }

            if (!BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                user.AccessFailedCount++;
                if (user.AccessFailedCount >= 3)
                {
                    user.AccessFailedCount = 0;
                    user.LockoutLevel = Math.Min(user.LockoutLevel + 1, 3);
                    var lockMinutes = user.LockoutLevel switch
                    {
                        1 => 1,
                        2 => 5,
                        _ => 10
                    };
                    user.LockoutEnd = now.AddMinutes(lockMinutes);
                    await _context.SaveChangesAsync();
                    ModelState.AddModelError(string.Empty,
                        $"Three incorrect attempts. Login is locked for {lockMinutes} minute(s).");
                }
                else
                {
                    await _context.SaveChangesAsync();
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                }
                return View(model);
            }

            // A successful login clears the current failed-attempt counter and lockout.
            user.AccessFailedCount = 0;
            user.LockoutLevel = 0;
            user.LockoutEnd = null;
            await _context.SaveChangesAsync();

            await SignInUserAsync(user);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return user.Role == UserRole.Staff
                ? RedirectToAction("Index", "AdminDashboard")
                : RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            ViewBag.ProfileImagePath = user.ProfileImagePath;
            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role.ToString(),
                RewardPoints = user.RewardPoints
            };
            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Email/Role/RewardPoints are display-only, so reload them for the redisplayed form
                var current = await _context.Users.FindAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
                if (current == null) return NotFound();
                model.Email = current.Email;
                model.Role = current.Role.ToString();
                model.RewardPoints = current.RewardPoints;
                return View(model);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProfilePicture(IFormFile? profilePicture)
        {
            if (profilePicture == null || profilePicture.Length == 0)
            {
                TempData["Error"] = "Please choose an image to upload.";
                return RedirectToAction(nameof(Profile));
            }

            const long maxBytes = 5 * 1024 * 1024;
            if (profilePicture.Length > maxBytes)
            {
                TempData["Error"] = "Profile pictures must be 5 MB or smaller.";
                return RedirectToAction(nameof(Profile));
            }

            var extension = Path.GetExtension(profilePicture.FileName).ToLowerInvariant();
            if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            {
                TempData["Error"] = "Only JPG, PNG and WEBP images are supported.";
                return RedirectToAction(nameof(Profile));
            }

            // Validate the actual file signature rather than trusting the browser's content type.
            await using var input = profilePicture.OpenReadStream();
            var header = new byte[12];
            var bytesRead = await input.ReadAsync(header.AsMemory(0, header.Length));
            bool validImage = extension switch
            {
                ".jpg" or ".jpeg" => bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => bytesRead >= 8 && header.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
                ".webp" => bytesRead >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF"
                    && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
                _ => false
            };
            if (!validImage)
            {
                TempData["Error"] = "The selected file is not a valid JPG, PNG or WEBP image.";
                return RedirectToAction(nameof(Profile));
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(uploadFolder);
            var fileName = $"{userId}_{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(uploadFolder, fileName);
            input.Position = 0;
            await using (var output = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await input.CopyToAsync(output);

            var oldImage = user.ProfileImagePath;
            user.ProfileImagePath = $"/uploads/profiles/{fileName}";
            await _context.SaveChangesAsync();
            await SignInUserAsync(user);

            // Remove only a previous file stored in this application's profile upload folder.
            if (!string.IsNullOrWhiteSpace(oldImage) && oldImage.StartsWith("/uploads/profiles/", StringComparison.Ordinal))
            {
                var oldName = Path.GetFileName(oldImage);
                var oldPath = Path.Combine(uploadFolder, oldName);
                if (System.IO.File.Exists(oldPath) && !string.Equals(oldPath, physicalPath, StringComparison.OrdinalIgnoreCase))
                    System.IO.File.Delete(oldPath);
            }

            TempData["Success"] = "Profile picture updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        public IActionResult AccessDenied() => View();

        private async Task SignInUserAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            if (!string.IsNullOrWhiteSpace(user.ProfileImagePath))
                claims.Add(new Claim("ProfileImagePath", user.ProfileImagePath));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }
    }
}