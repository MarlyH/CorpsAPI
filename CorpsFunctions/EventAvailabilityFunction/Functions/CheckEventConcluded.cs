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
    public async Task Run([TimerTrigger("0 0 0 * * *")] TimerInfo timer)  // run every minute for testing // runs at midnight UTC [TimerTrigger("0 */1 * * * *")]
    {
        _logger.LogInformation($"Check Event Concluded Function started at: {DateTime.Now}");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(_configuration.GetValue<string>("SqlConnection"));

        using var context = new AppDbContext(optionsBuilder.Options);

        var nzTimeZone = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, nzTimeZone));
        
        var eventsToUpdate = await context.Events
            .Where(e => e.StartDate < today && e.Status == EventStatus.Available)
            .ToListAsync();

        foreach (var ev in eventsToUpdate)
        {
            ev.Status = EventStatus.Concluded;
        }

        await context.SaveChangesAsync();

        _logger.LogInformation($"Updated {eventsToUpdate.Count} events to 'Concluded'");
    }
}
