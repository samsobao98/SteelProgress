using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteelProgress.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "WorkoutSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsInPerson",
                table: "WorkoutSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ClientId",
                table: "Routines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Uid = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    AvatarColor = table.Column<string>(type: "TEXT", nullable: false),
                    Goal = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clients_UserProfiles_UserId",
                        column: x => x.UserId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_ClientId",
                table: "WorkoutSessions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Routines_ClientId",
                table: "Routines",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_Uid",
                table: "Clients",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_UserId",
                table: "Clients",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Routines_Clients_ClientId",
                table: "Routines",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutSessions_Clients_ClientId",
                table: "WorkoutSessions",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Routines_Clients_ClientId",
                table: "Routines");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutSessions_Clients_ClientId",
                table: "WorkoutSessions");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutSessions_ClientId",
                table: "WorkoutSessions");

            migrationBuilder.DropIndex(
                name: "IX_Routines_ClientId",
                table: "Routines");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "IsInPerson",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Routines");
        }
    }
}
