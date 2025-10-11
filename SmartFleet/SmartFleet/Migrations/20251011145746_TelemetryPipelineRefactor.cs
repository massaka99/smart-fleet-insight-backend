using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartFleet.Migrations
{
    /// <inheritdoc />
    public partial class TelemetryPipelineRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_VehicleTelemetrySnapshots",
                table: "VehicleTelemetrySnapshots");

            migrationBuilder.RenameTable(
                name: "VehicleTelemetrySnapshots",
                newName: "VehicleStates");

            migrationBuilder.RenameIndex(
                name: "IX_VehicleTelemetrySnapshots_VehicleId",
                table: "VehicleStates",
                newName: "IX_VehicleStates_VehicleId");

            migrationBuilder.RenameIndex(
                name: "IX_VehicleTelemetrySnapshots_VehicleCode",
                table: "VehicleStates",
                newName: "IX_VehicleStates_VehicleCode");

            migrationBuilder.RenameIndex(
                name: "IX_VehicleTelemetrySnapshots_NumberPlate",
                table: "VehicleStates",
                newName: "IX_VehicleStates_NumberPlate");

            migrationBuilder.RenameColumn(
                name: "ReceivedAtUtc",
                table: "VehicleStates",
                newName: "UpdatedAtUtc");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VehicleStates",
                table: "VehicleStates",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "VehicleTelemetryDeadLetters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleTelemetryDeadLetters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VehicleTelemetryRawMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelemetryId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    VehicleCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleTelemetryRawMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTelemetryRawMessages_TelemetryId",
                table: "VehicleTelemetryRawMessages",
                column: "TelemetryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleTelemetryDeadLetters");

            migrationBuilder.DropTable(
                name: "VehicleTelemetryRawMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VehicleStates",
                table: "VehicleStates");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "VehicleStates",
                newName: "ReceivedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_VehicleStates_VehicleId",
                table: "VehicleStates",
                newName: "IX_VehicleTelemetrySnapshots_VehicleId");

            migrationBuilder.RenameIndex(
                name: "IX_VehicleStates_VehicleCode",
                table: "VehicleStates",
                newName: "IX_VehicleTelemetrySnapshots_VehicleCode");

            migrationBuilder.RenameIndex(
                name: "IX_VehicleStates_NumberPlate",
                table: "VehicleStates",
                newName: "IX_VehicleTelemetrySnapshots_NumberPlate");

            migrationBuilder.RenameTable(
                name: "VehicleStates",
                newName: "VehicleTelemetrySnapshots");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VehicleTelemetrySnapshots",
                table: "VehicleTelemetrySnapshots",
                column: "Id");
        }
    }
}
