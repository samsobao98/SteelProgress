using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteelProgress.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSharingAndPrescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsWarmup",
                table: "WorkoutSets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "WorkoutSets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Rpe",
                table: "WorkoutSets",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "WorkoutSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Feeling",
                table: "WorkoutSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsComplete",
                table: "WorkoutSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "WorkoutSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "WorkoutSessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "SimpleViewBlocks",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "TrainsSelf",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ViewMode",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AuthorName",
                table: "Routines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceUid",
                table: "Routines",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Routines",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Routines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "RoutineDays",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "GroupLabel",
                table: "RoutineDayExercises",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "RoutineDayExercises",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestSeconds",
                table: "RoutineDayExercises",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TargetPercent1RM",
                table: "RoutineDayExercises",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetRepsMax",
                table: "RoutineDayExercises",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetRir",
                table: "RoutineDayExercises",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TargetRpe",
                table: "RoutineDayExercises",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TargetWeight",
                table: "RoutineDayExercises",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tempo",
                table: "RoutineDayExercises",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "RoutineDayExercises",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "WarmupSets",
                table: "RoutineDayExercises",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Uid",
                table: "Exercises",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Las filas existentes reciben un Uid propio antes de crear los índices únicos
            // (formato de Guid en mayúsculas, como lo guarda EF Core en SQLite)
            foreach (var table in new[] { "Exercises", "Routines", "RoutineDays", "RoutineDayExercises", "WorkoutSessions" })
            {
                migrationBuilder.Sql(
                    $"UPDATE \"{table}\" SET \"Uid\" = upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-' || substr('89AB', 1 + (abs(random()) % 4), 1) || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6)));");
            }

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_Uid",
                table: "WorkoutSessions",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Routines_Uid",
                table: "Routines",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutineDays_Uid",
                table: "RoutineDays",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutineDayExercises_Uid",
                table: "RoutineDayExercises",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exercises_Uid",
                table: "Exercises",
                column: "Uid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkoutSessions_Uid",
                table: "WorkoutSessions");

            migrationBuilder.DropIndex(
                name: "IX_Routines_Uid",
                table: "Routines");

            migrationBuilder.DropIndex(
                name: "IX_RoutineDays_Uid",
                table: "RoutineDays");

            migrationBuilder.DropIndex(
                name: "IX_RoutineDayExercises_Uid",
                table: "RoutineDayExercises");

            migrationBuilder.DropIndex(
                name: "IX_Exercises_Uid",
                table: "Exercises");

            migrationBuilder.DropColumn(
                name: "IsWarmup",
                table: "WorkoutSets");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "WorkoutSets");

            migrationBuilder.DropColumn(
                name: "Rpe",
                table: "WorkoutSets");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "Feeling",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "IsComplete",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "SimpleViewBlocks",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "TrainsSelf",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "ViewMode",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "AuthorName",
                table: "Routines");

            migrationBuilder.DropColumn(
                name: "SourceUid",
                table: "Routines");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Routines");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Routines");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "RoutineDays");

            migrationBuilder.DropColumn(
                name: "GroupLabel",
                table: "RoutineDayExercises");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "RoutineDayExercises");

            migrationBuilder.DropColumn(
                name: "RestSeconds",
                table: "RoutineDayExercises");

            migrationBuilder.DropColumn(
                name: "TargetPercent1RM",
                table: "RoutineDayExercises");

            migrationBuilder.DropColumn(
                name: "TargetRepsMax",
                table: "RoutineDayExercises");

            migrationBuilder.DropColumn(
                name: "TargetRir",
                table: "RoutineDayExercises");

            migrationBuilder.DropColumn(
                name: "TargetRpe",
                table: "RoutineDayExercises");

            migrationBuilder.DropColumn(
                name: "TargetWeight",
                table: "RoutineDayExercises");

            migrationBuilder.DropColumn(
                name: "Tempo",
                table: "RoutineDayExercises");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "RoutineDayExercises");

            migrationBuilder.DropColumn(
                name: "WarmupSets",
                table: "RoutineDayExercises");

            migrationBuilder.DropColumn(
                name: "Uid",
                table: "Exercises");
        }
    }
}
