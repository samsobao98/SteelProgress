using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteelProgress.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeUidsUniquePerOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Routines_Uid",
                table: "Routines");

            migrationBuilder.DropIndex(
                name: "IX_Routines_UserId",
                table: "Routines");

            migrationBuilder.DropIndex(
                name: "IX_Exercises_Uid",
                table: "Exercises");

            migrationBuilder.DropIndex(
                name: "IX_Exercises_UserId",
                table: "Exercises");

            migrationBuilder.DropIndex(
                name: "IX_ExerciseMedia_ExerciseId",
                table: "ExerciseMedia");

            migrationBuilder.DropIndex(
                name: "IX_ExerciseMedia_Uid",
                table: "ExerciseMedia");

            migrationBuilder.CreateIndex(
                name: "IX_Routines_UserId_Uid",
                table: "Routines",
                columns: new[] { "UserId", "Uid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_UserId_Uid",
                table: "Exercises",
                columns: new[] { "UserId", "Uid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMedia_ExerciseId_Uid",
                table: "ExerciseMedia",
                columns: new[] { "ExerciseId", "Uid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Routines_UserId_Uid",
                table: "Routines");

            migrationBuilder.DropIndex(
                name: "IX_Exercises_UserId_Uid",
                table: "Exercises");

            migrationBuilder.DropIndex(
                name: "IX_ExerciseMedia_ExerciseId_Uid",
                table: "ExerciseMedia");

            migrationBuilder.CreateIndex(
                name: "IX_Routines_Uid",
                table: "Routines",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routines_UserId",
                table: "Routines",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_Uid",
                table: "Exercises",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_UserId",
                table: "Exercises",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMedia_ExerciseId",
                table: "ExerciseMedia",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseMedia_Uid",
                table: "ExerciseMedia",
                column: "Uid",
                unique: true);
        }
    }
}
