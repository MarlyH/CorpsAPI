using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorpsAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStrikeProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttendanceStrikeCount",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfLastStrike",
                table: "AspNetUsers",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttendanceStrikeCount",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DateOfLastStrike",
                table: "AspNetUsers");
        }
    }
}
