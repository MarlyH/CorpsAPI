namespace CorpsAPI.DTOs.Child
{
    public class ChildDto
    {
        public int ChildId { get; set; }
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public DateOnly DateOfBirth { get; set; }
        public string EmergencyContactName { get; set; } = default!;
        public string EmergencyContactPhone { get; set; } = default!;
        public int Age { get; set; }
    }

    public class CreateChildDto
    {
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public DateOnly DateOfBirth { get; set; }
        public string EmergencyContactName { get; set; } = default!;
        public string EmergencyContactPhone { get; set; } = default!;
    }

    public class UpdateChildDto : CreateChildDto { }
}
