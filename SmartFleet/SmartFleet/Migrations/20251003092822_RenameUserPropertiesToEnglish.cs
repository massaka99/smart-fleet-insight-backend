using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartFleet.Migrations
{
    /// <inheritdoc />
    public partial class RenameUserPropertiesToEnglish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Rolle",
                table: "Users",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "Fornavn",
                table: "Users",
                newName: "FirstName");

            migrationBuilder.RenameColumn(
                name: "Efternavn",
                table: "Users",
                newName: "LastName");

            migrationBuilder.RenameColumn(
                name: "Alder",
                table: "Users",
                newName: "Age");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Role",
                table: "Users",
                newName: "Rolle");

            migrationBuilder.RenameColumn(
                name: "FirstName",
                table: "Users",
                newName: "Fornavn");

            migrationBuilder.RenameColumn(
                name: "LastName",
                table: "Users",
                newName: "Efternavn");

            migrationBuilder.RenameColumn(
                name: "Age",
                table: "Users",
                newName: "Alder");
        }
    }
}
