using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpsAPI.Models
{
    public class UserMedicalCondition
    {
        public int Id { get; set; }
        public string UserId { get; set; } = default!;
        public AppUser User { get; set; } = default!;
        public string Name { get; set; } = default!;     // e.g., "Peanut allergy"
        public string? Notes { get; set; }               // optional details
        public bool IsAllergy { get; set; }
        
    }
}