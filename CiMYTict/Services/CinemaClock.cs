namespace CiMYTict.Services
{
    /// <summary>
    /// Single source of truth for time handling.
    /// System timestamps (CreatedAt, LockExpiresAt, PaidAt) are stored in UTC.
    /// Showtimes, promo dates and cancellation rules use the cinema's local time (Malaysia, UTC+8).
    /// </summary>
    public static class CinemaClock
    {
        private static readonly TimeZoneInfo Zone = ResolveZone();

        private static TimeZoneInfo ResolveZone()
        {
            foreach (var id in new[] { "Asia/Kuala_Lumpur", "Singapore Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("MYT", TimeSpan.FromHours(8), "MYT", "MYT");
        }

        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);
        public static TimeSpan Offset => Zone.GetUtcOffset(DateTime.UtcNow);
        public static DateTime Today => Now.Date;

        public static DateTime ToLocal(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    }
}
