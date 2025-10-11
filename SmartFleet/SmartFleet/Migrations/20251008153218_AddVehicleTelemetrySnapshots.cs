using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartFleet.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleTelemetrySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleTelemetrySnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TelemetryId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    VehicleCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NumberPlate = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    FuelType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FuelUnit = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    FuelCapacity = table.Column<double>(type: "double precision", nullable: false),
                    FuelLevel = table.Column<double>(type: "double precision", nullable: false),
                    FuelLevelPercent = table.Column<double>(type: "double precision", nullable: false),
                    FuelConsumptionPer100Km = table.Column<double>(type: "double precision", nullable: false),
                    OdometerKm = table.Column<double>(type: "double precision", nullable: false),
                    Co2EmissionKg = table.Column<double>(type: "double precision", nullable: false),
                    RouteId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RouteSummary = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RouteDistanceKm = table.Column<double>(type: "double precision", nullable: false),
                    BaseSpeedKmh = table.Column<double>(type: "double precision", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    SpeedKmh = table.Column<double>(type: "double precision", nullable: false),
                    HeadingDeg = table.Column<double>(type: "double precision", nullable: false),
                    DistanceTravelledM = table.Column<double>(type: "double precision", nullable: false),
                    DistanceRemainingM = table.Column<double>(type: "double precision", nullable: false),
                    Progress = table.Column<double>(type: "double precision", nullable: false),
                    EtaSeconds = table.Column<double>(type: "double precision", nullable: false),
                    StopsJson = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleTelemetrySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleTelemetrySnapshots_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTelemetrySnapshots_NumberPlate",
                table: "VehicleTelemetrySnapshots",
                column: "NumberPlate");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTelemetrySnapshots_VehicleCode",
                table: "VehicleTelemetrySnapshots",
                column: "VehicleCode");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleTelemetrySnapshots_VehicleId",
                table: "VehicleTelemetrySnapshots",
                column: "VehicleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleTelemetrySnapshots");
        }
    }
}
