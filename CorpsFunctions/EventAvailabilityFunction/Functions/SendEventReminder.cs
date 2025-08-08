using CorpsAPI.Data;
using CorpsAPI.Models;
using CorpsAPI.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class SendEventReminder
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;
    private readonly NotificationService _notificationService;

    public SendEventReminder(ILoggerFactory loggerFactory, IConfiguration configuration, NotificationService notificationService /*, EmailService emailService*/ )
    {
        _logger = loggerFactory.CreateLogger<SendEventReminder>();
        _configuration = configuration;
        _notificationService = notificationService;
    }

    [Function("SendEventReminder")]
    public async Task Run([TimerTrigger("0 */30 * * * *")] TimerInfo timer) // run every 30mins
    {
        _logger.LogInformation($"Event Reminder Function started at: {DateTime.Now}");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(_configuration.GetValue<string>("SqlConnection"));

        using var context = new AppDbContext(optionsBuilder.Options);

        var nzTimeZone = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, nzTimeZone));
        var now = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, nzTimeZone));

        // get e where startdate == today and (starttime > now && starttime < now +2hrs)
        var eventsToRemind = await context.Events
            .Where(e => e.StartDate == today 
                && e.IsReminded == false
                && e.Status == EventStatus.Available
                && e.StartTime > now
                && e.StartTime <= now.AddHours(2))
            .Include(e => e.Bookings)
                .ThenInclude(b => b.User)
            .ToListAsync();
        _logger.LogInformation($"Checking for events between {now} and {now.AddHours(2)}");

        var emailBody = $@"
            <p>event beginning soon btw</p>";

        foreach (var ev in eventsToRemind)
        {
            foreach (var booking in ev.Bookings)
            {
                var userEmail = booking.User!.Email!;
                try
                {
                    await _notificationService.SendCrossPlatformNotificationAsync(booking.UserId!, "event reminder", "test");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }
            }
            ev.IsReminded = true;
        }

        await context.SaveChangesAsync();
        _logger.LogInformation($"{eventsToRemind.Count} events had reminder notifications sent.");
    }
}
