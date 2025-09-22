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
        public bool HasMedicalConditions { get; set; }
        public List<MedicalConditionDto>? MedicalConditions { get; set; }
    }

    public class CreateChildDto
    {
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public DateOnly DateOfBirth { get; set; }
        public string EmergencyContactName { get; set; } = default!;
        public string EmergencyContactPhone { get; set; } = default!;
        public bool HasMedicalConditions { get; set; }
        public List<MedicalConditionDto>? MedicalConditions { get; set; }
    }

    public class UpdateChildDto : CreateChildDto { }
}
