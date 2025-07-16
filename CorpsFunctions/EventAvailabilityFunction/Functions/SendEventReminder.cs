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
    //private readonly EmailService _emailService;

    public SendEventReminder(ILoggerFactory loggerFactory, IConfiguration configuration/*, EmailService emailService*/)
    {
        _logger = loggerFactory.CreateLogger<SendEventReminder>();
        _configuration = configuration;
        //_emailService = emailService;
    }

    [Function("SendEventReminder")]
    public async Task Run([TimerTrigger("0 0 0 * * *")] TimerInfo timer)  // run every minute for testing // runs at midnight UTC [TimerTrigger("0 0 0 * * *")]
    {
        _logger.LogInformation($"Event Reminder Function started at: {DateTime.Now}");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(_configuration.GetValue<string>("SqlConnection"));

        using var context = new AppDbContext(optionsBuilder.Options);

        var nzTimeZone = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, nzTimeZone));
        var now = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, nzTimeZone));

        // get e where startdate == today and (starttime > now && starttime < now +2hrs)
        // run every 30mins?
        var eventsToRemind = await context.Events
            .Where(e => e.StartDate == today 
                && e.IsReminded == false
                && e.Status == EventStatus.Available
                && e.StartTime > now
                && e.StartTime <= now.AddHours(2))
            .Include(e => e.Bookings)
                .ThenInclude(b => b.User)
            .ToListAsync();

        var emailBody = $@"
            <p>event beginning soon btw</p>";

        foreach (var ev in eventsToRemind)
        {
            foreach (var booking in ev.Bookings)
            {
                var userEmail = booking.User!.Email!;
                try
                {
                    //await _emailService.SendEmailAsync(userEmail, "Reminder", emailBody);
                    _logger.LogInformation($"Reminder email sent to '{userEmail}'");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }
            }
            ev.IsReminded = true;
        }

        await context.SaveChangesAsync();
        _logger.LogInformation($"{eventsToRemind.Count} events had reminder emails sent.");
    }
}
