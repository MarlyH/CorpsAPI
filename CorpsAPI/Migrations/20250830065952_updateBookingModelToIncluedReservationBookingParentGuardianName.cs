using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorpsAPI.Migrations
{
    /// <inheritdoc />
    public partial class updateBookingModelToIncluedReservationBookingParentGuardianName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReservedBookingParentGuardianName",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservedBookingParentGuardianName",
                table: "Bookings");
        }
    }
}
