using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SteelProgress.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "UserProfiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // La antigua «vista entrenador» (ViewMode = 1) pasa a ser la vista detallada. Solo los
            // perfiles que ya tienen clientes son de entrenador; los demás siguen siendo personales,
            // así sus propios entrenos se siguen viendo.
            migrationBuilder.Sql(
                "UPDATE UserProfiles SET Kind = 1 " +
                "WHERE ViewMode = 1 AND EXISTS (SELECT 1 FROM Clients WHERE Clients.UserId = UserProfiles.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "UserProfiles");
        }
    }
}
