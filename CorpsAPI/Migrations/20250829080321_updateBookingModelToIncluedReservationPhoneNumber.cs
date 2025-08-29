using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorpsAPI.Migrations
{
    /// <inheritdoc />
    public partial class updateBookingModelToIncluedReservationPhoneNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReservedBookingPhone",
                table: "Bookings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservedBookingPhone",
                table: "Bookings");
        }
    }
}
