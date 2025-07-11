using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorpsAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Waitlists_EventId",
                table: "Waitlists",
                column: "EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_Waitlists_AspNetUsers_UserId",
                table: "Waitlists",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Waitlists_Events_EventId",
                table: "Waitlists",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "EventId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Waitlists_AspNetUsers_UserId",
                table: "Waitlists");

            migrationBuilder.DropForeignKey(
                name: "FK_Waitlists_Events_EventId",
                table: "Waitlists");

            migrationBuilder.DropIndex(
                name: "IX_Waitlists_EventId",
                table: "Waitlists");
        }
    }
}
