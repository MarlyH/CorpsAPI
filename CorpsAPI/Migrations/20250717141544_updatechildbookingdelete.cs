using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorpsAPI.Migrations
{
    /// <inheritdoc />
    public partial class updatechildbookingdelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Children_ChildId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Children_AspNetUsers_ParentUserId",
                table: "Children");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Children_ChildId",
                table: "Bookings",
                column: "ChildId",
                principalTable: "Children",
                principalColumn: "ChildId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Children_AspNetUsers_ParentUserId",
                table: "Children",
                column: "ParentUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Children_ChildId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Children_AspNetUsers_ParentUserId",
                table: "Children");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Children_ChildId",
                table: "Bookings",
                column: "ChildId",
                principalTable: "Children",
                principalColumn: "ChildId");

            migrationBuilder.AddForeignKey(
                name: "FK_Children_AspNetUsers_ParentUserId",
                table: "Children",
                column: "ParentUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
