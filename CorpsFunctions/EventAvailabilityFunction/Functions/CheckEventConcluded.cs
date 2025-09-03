using CorpsAPI.Data;
using CorpsAPI.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class CheckEventConcluded
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;

    public CheckEventConcluded(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _logger = loggerFactory.CreateLogger<CheckEventConcluded>();
        _configuration = configuration;
    }

    [Function("CheckEventConcluded")]
    public async Task Run([TimerTrigger("0 0 11 * * *")] TimerInfo timer)  // 11:00 UTC
    {
        _logger.LogInformation($"Check Event Concluded Function started at: {DateTime.UtcNow:u}");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(_configuration.GetValue<string>("SqlConnection"));

        using var context = new AppDbContext(optionsBuilder.Options);

        var nzTimeZone = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
        var todayNz = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, nzTimeZone));

        // Conclude any event whose StartDate is strictly before "today" in NZT
        var eventsToUpdate = await context.Events
            .Where(e => e.StartDate < todayNz && e.Status == EventStatus.Available)
            .Include(e => e.Bookings)
                .ThenInclude(b => b.User)
            .ToListAsync();

        foreach (var ev in eventsToUpdate)
        {
            // Unattended = never cancelled/striked/checked out.
            // We strike both Booked (never arrived) and CheckedIn (arrived but never checked out).
            var unattendedBookings = ev.Bookings
                .Where(b =>
                    (b.Status == BookingStatus.Booked || b.Status == BookingStatus.CheckedIn))
                .ToList();

            if (unattendedBookings.Count == 0)
            {
                ev.Status = EventStatus.Concluded;
                continue;
            }

            // 1) Mark ALL unattended bookings as Striked (including reservations & null-user)
            foreach (var booking in unattendedBookings)
            {
                booking.Status = BookingStatus.Striked;
                booking.SeatNumber = null; // free the seat
            }

            // 2) Compute which USERS to penalize:
            //    - Skip reservations (ReservedBookingAttendeeName is set) so staff accounts aren't penalized.
            //    - Skip null users (defensive).
            //    - Group by UserId so a parent with many no-show children gets ONLY ONE strike for this event.
            var penalizableUserIds = unattendedBookings
                .Where(b => string.IsNullOrWhiteSpace(b.ReservedBookingAttendeeName) && b.User != null)
                .Select(b => b.User!.Id)
                .Distinct()
                .ToList();

            if (penalizableUserIds.Count > 0)
            {
                // Load only the affected users attached to those unattended bookings
                var usersToPenalize = unattendedBookings
                    .Where(b => b.User != null && penalizableUserIds.Contains(b.User!.Id))
                    .Select(b => b.User!)
                    .Distinct()
                    .ToList();

                foreach (var user in usersToPenalize)
                {
                    user.AttendanceStrikeCount++;
                    user.DateOfLastStrike = todayNz;
                }
            }

            ev.Status = EventStatus.Concluded;
        }

        await context.SaveChangesAsync();

        _logger.LogInformation($"Concluded {eventsToUpdate.Count} events and applied strikes where applicable.");
    }
}
