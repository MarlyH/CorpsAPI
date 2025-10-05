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

        // Time zone (Windows & Linux safe)
        TimeZoneInfo nzTz;
        try { nzTz = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time"); }
        catch { nzTz = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland"); }

        var todayNz = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, nzTz));

        var eventsToUpdate = await context.Events
        .Where(e => e.StartDate < todayNz && e.Status == EventStatus.Available)
        .Include(e => e.Bookings).ThenInclude(b => b.User)
        .ToListAsync();

        foreach (var ev in eventsToUpdate)
        {
            // unattended before we mutate anything
            var unattended = ev.Bookings
                .Where(b => b.Status == BookingStatus.Booked || b.Status == BookingStatus.CheckedIn)
                .ToList();

            // users who were already striked on this event BEFORE we change anything now
            var alreadyStrikedUserIds = ev.Bookings
                .Where(b => b.Status == BookingStatus.Striked && b.User != null)
                .Select(b => b.User!.Id)
                .Distinct()
                .ToHashSet();

            // mark unattended bookings as Striked (mutation happens AFTER the check above)
            foreach (var b in unattended)
            {
                b.Status = BookingStatus.Striked;
                b.SeatNumber = null;
            }

            // users to penalize (skip reservations/null users)
            var penalizableUserIds = unattended
                .Where(b => string.IsNullOrWhiteSpace(b.ReservedBookingAttendeeName) && b.User != null)
                .Select(b => b.User!.Id)
                .Distinct()
                .Where(id => !alreadyStrikedUserIds.Contains(id)) // avoid double-penalizing if this event already produced strikes
                .ToList();

            if (penalizableUserIds.Count > 0)
            {
                // load users and increment once each
                var users = await context.Users
                    .Where(u => penalizableUserIds.Contains(u.Id))
                    .ToListAsync();

                foreach (var u in users)
                {
                    u.AttendanceStrikeCount++;
                    u.DateOfLastStrike = todayNz;
                }
            }

            ev.Status = EventStatus.Concluded;
        }


        await context.SaveChangesAsync();


        _logger.LogInformation($"Concluded {eventsToUpdate.Count} events and applied strikes where applicable.");
    }
}
