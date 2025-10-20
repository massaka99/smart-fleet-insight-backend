using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartFleet.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedTablesV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_Users_DriverId",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_DriverId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "DriverId",
                table: "Vehicles");

            migrationBuilder.AddColumn<int>(
                name: "VehicleId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VehicleTelemetries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TelemetryId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VehicleId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NumberPlate = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Brand = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FuelType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FuelUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FuelCapacity = table.Column<double>(type: "double precision", nullable: true),
                    BatteryCapacity = table.Column<double>(type: "double precision", nullable: true),
                    FuelLevel = table.Column<double>(type: "double precision", nullable: false),
                    FuelLevelPercent = table.Column<double>(type: "double precision", nullable: false),
                    FuelConsumptionPer100Km = table.Column<double>(type: "double precision", nullable: false),
                    OdometerKm = table.Column<double>(type: "double precision", nullable: false),
                    Co2EmissionKg = table.Column<double>(type: "double precision", nullable: false),
                    RouteId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RouteSummary = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RouteDistanceKm = table.Column<double>(type: "double precision", nullable: false),
                    BaseSpeedKmh = table.Column<double>(type: "double precision", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PositionLat = table.Column<double>(type: "double precision", nullable: false),
                    PositionLon = table.Column<double>(type: "double precision", nullable: false),
                    SpeedKmh = table.Column<double>(type: "double precision", nullable: false),
                    HeadingDeg = table.Column<double>(type: "double precision", nullable: false),
                    DistanceTravelledM = table.Column<double>(type: "double precision", nullable: false),
                    DistanceRemainingM = table.Column<double>(type: "double precision", nullable: false),
                    Progress = table.Column<double>(type: "double precision", nullable: false),
                    EtaSeconds = table.Column<double>(type: "double precision", nullable: false),
                    StopsJson = table.Column<string>(type: "jsonb", nullable: false),
                    RawPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleTelemetries", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "VehicleId",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Users_VehicleId",
                table: "Users",
                column: "VehicleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTelemetries_TelemetryId",
                table: "VehicleTelemetries",
                column: "TelemetryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTelemetries_VehicleId_TimestampUtc",
                table: "VehicleTelemetries",
                columns: new[] { "VehicleId", "TimestampUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Vehicles_VehicleId",
                table: "Users",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Vehicles_VehicleId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "VehicleTelemetries");

            migrationBuilder.DropIndex(
                name: "IX_Users_VehicleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "DriverId",
                table: "Vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_DriverId",
                table: "Vehicles",
                column: "DriverId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_Users_DriverId",
                table: "Vehicles",
                column: "DriverId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
