using System.ComponentModel.DataAnnotations;
using CorpsAPI.Models;

namespace CorpsAPI.DTOs
{
    public class CreateEventDto
    {
        public int LocationId { get; set; }
        public EventSessionType SessionType { get; set; }
        public DateOnly StartDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public DateOnly AvailableDate { get; set; }
        public string SeatingMapImgSrc { get; set; } = default!;
        public int TotalSeats { get; set; }
        [MaxLength(500)]
        public string? Description { get; set; }
        [MaxLength(100)]
        public string? Address { get; set; }
    }

    public class GetAllEventsDto
    {
        public GetAllEventsDto(Event e)
        {
            EventId = e.EventId;
            LocationName = e.Location!.Name;
            SessionType = e.SessionType;
            StartDate = e.StartDate;
            AvailableDate = e.AvailableDate;
            StartTime = e.StartTime;
            EndTime = e.EndTime;
            Description = e.Description;
            Address = e.Address;
            AvailbleSeatsCount = e.TotalSeats - e.Bookings.Count;
        }
        public int EventId { get; set; }
        public string LocationName { get; set; } = default!;
        public EventSessionType SessionType { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly AvailableDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string? Description { get; set; }
        public string? Address { get; set; }
        public int AvailbleSeatsCount { get; set; }
    }

    public class GetEventDto
    {
        public GetEventDto(Event e)
        {
            EventId = e.EventId;
            LocationName = e.Location!.Name;
            SessionType = e.SessionType;
            StartDate = e.StartDate;
            AvailableDate = e.AvailableDate;
            StartTime = e.StartTime;
            EndTime = e.EndTime;
            Description = e.Description;
            Address = e.Address;
            TotalSeatsCount = e.TotalSeats;
            AvailbleSeatsCount = e.AvailableSeats;
            SeatingMapImgSrc = e.SeatingMapImgSrc;

            // Get available seat numbers
            var bookedSeats = e.Bookings?.Select(b => b.SeatNumber).ToHashSet() ?? new HashSet<int>();
            for (int i = 1; i <= e.TotalSeats; i++)
            {
                if (!bookedSeats.Contains(i))
                    AvailableSeats.Add(i);
            }
        }
        public int EventId { get; set; }
        public string LocationName { get; set; } = default!;
        public EventSessionType SessionType { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly AvailableDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string? Description { get; set; }
        public string? Address { get; set; }
        public int TotalSeatsCount { get; set; }
        public int AvailbleSeatsCount { get; set; }
        public List<int> AvailableSeats { get; set; } = new();
        public string SeatingMapImgSrc { get; set; } = default!;
    }

    public class LocationDto
    {
        public int LocationId { get; set; }
        public string Name { get; set; } = default!;
        public string? MascotImgSrc { get; set; }
    }
}
