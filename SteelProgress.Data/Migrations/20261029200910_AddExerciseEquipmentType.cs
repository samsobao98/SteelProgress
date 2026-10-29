using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteelProgress.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseEquipmentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EquipmentType",
                table: "Exercises",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EquipmentType",
                table: "Exercises");
        }
    }
}
