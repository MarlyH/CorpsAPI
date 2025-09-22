using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpsAPI.Models
{
    public class ChildMedicalCondition
    {
        public int Id { get; set; }
        public int ChildId { get; set; }
        public Child Child { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Notes { get; set; }
        public bool IsAllergy { get; set; }
    }
}