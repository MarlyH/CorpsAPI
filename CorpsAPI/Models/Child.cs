using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CorpsAPI.Models
{
    public enum AgeGroup
    {
        None,
        Kids8To11,
        Teens12To15
    }

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

        [NotMapped]
        public int Age
        {
            get
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                var age = today.Year - DateOfBirth.Year;

                if (today.Month < DateOfBirth.Month || (today.Month == DateOfBirth.Month && today.Day < DateOfBirth.Day))
                    age--;

                return age;
            }
        }

        [NotMapped]
        public AgeGroup AgeGroup
        {
            get
            {
                if (Age >= 8 && Age <= 11)
                    return AgeGroup.Kids8To11;
                else if (Age >= 12 && Age <= 15)
                    return AgeGroup.Teens12To15;
                else
                    return AgeGroup.None;
            }
        }

        [NotMapped]
        public string AgeGroupLabel
        {
            get
            {
                return AgeGroup switch
                {
                    AgeGroup.Kids8To11 => "Kids Ages 8 to 11 (G and PG games only)",
                    AgeGroup.Teens12To15 => "Teens Ages 12 to 15 (M rated games allowed)",
                    _ => "Age not eligible for events"
                };
            }
        }
    }
}
