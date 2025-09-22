using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorpsAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddedMedicalDetailsForChildrenAndTeenUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasMedicalConditions",
                table: "Children",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasMedicalConditions",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ChildMedicalConditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAllergy = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildMedicalConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildMedicalConditions_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "ChildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMedicalConditions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsAllergy = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMedicalConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMedicalConditions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildMedicalConditions_ChildId",
                table: "ChildMedicalConditions",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMedicalConditions_UserId",
                table: "UserMedicalConditions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChildMedicalConditions");

            migrationBuilder.DropTable(
                name: "UserMedicalConditions");

            migrationBuilder.DropColumn(
                name: "HasMedicalConditions",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "HasMedicalConditions",
                table: "AspNetUsers");
        }
    }
}
