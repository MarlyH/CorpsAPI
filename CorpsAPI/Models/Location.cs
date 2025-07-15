using System.ComponentModel.DataAnnotations;

namespace CorpsAPI.Models
{
    public class Location
    {
        [Key]
        public int LocationId { get; set; }
        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = default!;
        public string? MascotImgSrc { get; set; }
        public List<Event> Events { get; set; } = new();
    }
}
