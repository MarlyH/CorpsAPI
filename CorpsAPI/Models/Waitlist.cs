using System.ComponentModel.DataAnnotations.Schema;

namespace CorpsAPI.Models
{
    public class Waitlist
    {
        [ForeignKey("User")]
        public string UserId { get; set; } = default!;
        public AppUser? User { get; set; }
        [ForeignKey("Event")]
        public int EventId { get; set; }
        public Event? Event { get; set; }

        //public DateTime Timestamp { get; set; }
    }
}
