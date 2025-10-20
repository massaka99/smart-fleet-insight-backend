using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartFleet.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedTablesV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleTelemetries");

            migrationBuilder.AddColumn<double>(
                name: "BaseSpeedKmh",
                table: "Vehicles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "BatteryCapacity",
                table: "Vehicles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "Vehicles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "DistanceRemainingM",
                table: "Vehicles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DistanceTravelledM",
                table: "Vehicles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "EtaSeconds",
                table: "Vehicles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Vehicles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FuelConsumptionPer100Km",
                table: "Vehicles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "FuelLevelPercent",
                table: "Vehicles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "FuelUnit",
                table: "Vehicles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "HeadingDeg",
                table: "Vehicles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTelemetryAtUtc",
                table: "Vehicles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Vehicles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Vehicles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Progress",
                table: "Vehicles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RouteDistanceKm",
                table: "Vehicles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "RouteId",
                table: "Vehicles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RouteSummary",
                table: "Vehicles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SpeedKmh",
                table: "Vehicles",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Vehicles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_ExternalId",
                table: "Vehicles",
                column: "ExternalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_ExternalId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "BaseSpeedKmh",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "BatteryCapacity",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "DistanceRemainingM",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "DistanceTravelledM",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "EtaSeconds",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FuelConsumptionPer100Km",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FuelLevelPercent",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FuelUnit",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "HeadingDeg",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "LastTelemetryAtUtc",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Progress",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "RouteDistanceKm",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "RouteId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "RouteSummary",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "SpeedKmh",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Vehicles");

            migrationBuilder.CreateTable(
                name: "VehicleTelemetries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseSpeedKmh = table.Column<double>(type: "double precision", nullable: false),
                    BatteryCapacity = table.Column<double>(type: "double precision", nullable: true),
                    Brand = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Co2EmissionKg = table.Column<double>(type: "double precision", nullable: false),
                    DistanceRemainingM = table.Column<double>(type: "double precision", nullable: false),
                    DistanceTravelledM = table.Column<double>(type: "double precision", nullable: false),
                    EtaSeconds = table.Column<double>(type: "double precision", nullable: false),
                    FuelCapacity = table.Column<double>(type: "double precision", nullable: true),
                    FuelConsumptionPer100Km = table.Column<double>(type: "double precision", nullable: false),
                    FuelLevel = table.Column<double>(type: "double precision", nullable: false),
                    FuelLevelPercent = table.Column<double>(type: "double precision", nullable: false),
                    FuelType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FuelUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    HeadingDeg = table.Column<double>(type: "double precision", nullable: false),
                    NumberPlate = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OdometerKm = table.Column<double>(type: "double precision", nullable: false),
                    PositionLat = table.Column<double>(type: "double precision", nullable: false),
                    PositionLon = table.Column<double>(type: "double precision", nullable: false),
                    Progress = table.Column<double>(type: "double precision", nullable: false),
                    RawPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RouteDistanceKm = table.Column<double>(type: "double precision", nullable: false),
                    RouteId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RouteSummary = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SpeedKmh = table.Column<double>(type: "double precision", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StopsJson = table.Column<string>(type: "jsonb", nullable: false),
                    TelemetryId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VehicleId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleTelemetries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTelemetries_TelemetryId",
                table: "VehicleTelemetries",
                column: "TelemetryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTelemetries_VehicleId_TimestampUtc",
                table: "VehicleTelemetries",
                columns: new[] { "VehicleId", "TimestampUtc" });
        }
    }
}
