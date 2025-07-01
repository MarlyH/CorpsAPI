using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CorpsAPI.Models
{
    public class Child
    {
        [Key]
        public int ChildId { get; set; }
        [Required]
        public DateOnly DateOfBirth { get; set; }
        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = default!;
        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = default!;
        [Required]
        [MaxLength(50)]
        public string EmergencyContactName { get; set; } = default!;
        [Required]
        public string EmergencyContactPhone { get; set; } = default!;
        
        [ForeignKey("ParentUser")]
        public string ParentUserId { get; set; } = default!;
        public AppUser ParentUser { get; set; } = default!;
    }
}