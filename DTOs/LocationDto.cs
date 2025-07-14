using CorpsAPI.Models;

namespace CorpsAPI.DTOs
{
    public class GetLocationDto
    {
        public GetLocationDto(Location location)
        {
            LocationId = location.LocationId;
            Name = location.Name;
            MascotImgSrc = location.MascotImgSrc;
        }
        public int LocationId { get; set; }
        public string Name { get; set; } = default!;
        public string? MascotImgSrc { get; set; }
    }

    public class GetAllLocationsDto
    {
        public GetAllLocationsDto(Location location)
        {
            LocationId = location.LocationId;
            Name = location.Name;
        }
        public int LocationId { get; set; }
        public string Name { get; set; } = default!;
    }
}
