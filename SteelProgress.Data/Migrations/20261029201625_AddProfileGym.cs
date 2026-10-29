using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteelProgress.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileGym : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BarWeight",
                table: "UserProfiles",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GymPlates",
                table: "UserProfiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BarWeight",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "GymPlates",
                table: "UserProfiles");
        }
    }
}
