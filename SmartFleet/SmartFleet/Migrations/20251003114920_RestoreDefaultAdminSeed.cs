using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartFleet.Migrations
{
    /// <inheritdoc />
    public partial class RestoreDefaultAdminSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1000);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Age", "FirstName", "LastName", "PasswordHash", "Role" },
                values: new object[] { 1, 35, "Default", "Admin", "AQAAAAIAAYagAAAAEOCJVUh+L5Pygby4OYlYZtLtrN/TrziEgkz6euPJtg4/c5uRcY1y6TrWBJF3+rt5Cg==", 2 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Age", "FirstName", "LastName", "PasswordHash", "Role" },
                values: new object[] { 1000, 35, "Default", "Admin", "AQAAAAIAAYagAAAAEOCJVUh+L5Pygby4OYlYZtLtrN/TrziEgkz6euPJtg4/c5uRcY1y6TrWBJF3+rt5Cg==", 2 });
        }
    }
}
