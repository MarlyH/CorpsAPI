using CorpsAPI.Data;
using CorpsAPI.Models;
using CorpsAPI.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class CheckEventConcluded
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;

    public CheckEventConcluded(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _logger = loggerFactory.CreateLogger<CheckEventConcluded>();
        _configuration = configuration;
    }

    private static List<string> ParseEventImageUrls(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(trimmed);
                if (parsed != null)
                {
                    return parsed
                        .Where(url => !string.IsNullOrWhiteSpace(url))
                        .Select(url => url.Trim())
                        .ToList();
                }
            }
            catch
            {
                // Backward compatibility: treat invalid JSON as a legacy single URL string.
            }
        }

        return new List<string> { trimmed };
    }

    private async Task<bool> TryDeleteEventImagesAsync(string? eventImageImgSrcRaw)
    {
        var imageUrls = ParseEventImageUrls(eventImageImgSrcRaw);
        if (imageUrls.Count == 0)
            return true;

        var storage = new AzureStorageService(_configuration);
        var allDeleted = true;

        foreach (var imageUrl in imageUrls)
        {
            try
            {
                await storage.DeleteImageAsync(imageUrl);
            }
            catch (Exception ex)
            {
                allDeleted = false;
                _logger.LogWarning(ex, "Failed deleting event image blob: {ImageUrl}", imageUrl);
            }
        }

        return allDeleted;
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

        var bookableEventsToConclude = await context.Events
            .Where(e => e.RequiresBooking && e.StartDate < todayNz && e.Status == EventStatus.Available)
            .Include(e => e.Bookings)
                .ThenInclude(b => b.User)
            .ToListAsync();

        foreach (var ev in bookableEventsToConclude)
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

        // Non-bookable listing expiry:
        // once listing period ends, remove from feeds and cleanup images from blob storage.
        var contentEventsToConclude = await context.Events
            .Where(e =>
                !e.RequiresBooking &&
                e.Status != EventStatus.Concluded &&
                e.Status != EventStatus.Cancelled &&
                ((e.EndDate.HasValue && e.EndDate.Value < todayNz) ||
                 (!e.EndDate.HasValue && e.StartDate < todayNz)))
            .ToListAsync();

        var contentImageDeleteFailures = 0;
        foreach (var ev in contentEventsToConclude)
        {
            var deleted = await TryDeleteEventImagesAsync(ev.EventImageImgSrc);
            if (deleted)
            {
                ev.EventImageImgSrc = null;
            }
            else
            {
                contentImageDeleteFailures++;
            }

            ev.Status = EventStatus.Concluded;
        }

        // Retry cleanup for already-concluded non-bookable listings that still have image references.
        var staleConcludedImageRefs = await context.Events
            .Where(e =>
                !e.RequiresBooking &&
                e.Status == EventStatus.Concluded &&
                !string.IsNullOrWhiteSpace(e.EventImageImgSrc))
            .ToListAsync();

        var staleCleanupSuccess = 0;
        foreach (var ev in staleConcludedImageRefs)
        {
            if (await TryDeleteEventImagesAsync(ev.EventImageImgSrc))
            {
                ev.EventImageImgSrc = null;
                staleCleanupSuccess++;
            }
        }

        await context.SaveChangesAsync();

        _logger.LogInformation(
            "Concluded {BookableCount} bookable events. Concluded {ContentCount} content listings (image delete failures: {ContentImageDeleteFailures}). Stale image cleanup successes: {StaleCleanupSuccess}.",
            bookableEventsToConclude.Count,
            contentEventsToConclude.Count,
            contentImageDeleteFailures,
            staleCleanupSuccess
        );
    }
}
