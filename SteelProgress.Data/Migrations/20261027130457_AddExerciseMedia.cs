using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteelProgress.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExerciseMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExerciseMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExerciseId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    Caption = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseMedia_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExerciseMedia");
        }
    }
}
