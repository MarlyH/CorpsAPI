using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CorpsAPI.Data;
using CorpsAPI.Models;

public class CheckEventAvailability
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;

    public CheckEventAvailability(ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _logger = loggerFactory.CreateLogger<CheckEventAvailability>();
        _configuration = configuration;
    }

    [Function("CheckEventAvailability")]
    public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo timer)  // run every minute for testing // runs at midnight UTC [TimerTrigger("0 0 0 * * *")]
    {
        _logger.LogInformation($"Check Event Availability Function started at: {DateTime.UtcNow}");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(_configuration.GetValue<string>("SqlConnection"));

        using var context = new AppDbContext(optionsBuilder.Options);

        var nzTimeZone = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, nzTimeZone));

        var eventsToUpdate = await context.Events
            .Where(e => e.AvailableDate <= today && e.Status == EventStatus.Unavailable)
            .ToListAsync();

        foreach (var ev in eventsToUpdate)
        {
            ev.Status = EventStatus.Available;
        }

        await context.SaveChangesAsync();

        _logger.LogInformation($"Updated {eventsToUpdate.Count} events to 'Available'");
    }
}
