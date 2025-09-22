using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpsAPI.DTOs
{
    public class UpdateMedicalRequestDto
    {
        public bool HasMedicalConditions { get; set; }
        public List<MedicalConditionDto> Conditions { get; set; } = new();
    }
}