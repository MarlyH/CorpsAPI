using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpsAPI.DTOs
{
    public class MedicalConditionDto
    {
        public int? Id { get; set; } // null when creating
        public string Name { get; set; } = default!;
        public string? Notes { get; set; }
        public bool IsAllergy { get; set; }
    }
}