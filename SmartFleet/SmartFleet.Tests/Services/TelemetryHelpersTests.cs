using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmartFleet.Models;
using SmartFleet.Options;
using SmartFleet.Services.Telemetry;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace SmartFleet.Tests.Services;

public class TelemetryHelpersTests
{
    [Fact]
    public void NormalizeVehicleTelemetry_ClampsValues()
    {
        var vehicle = new Vehicle
        {
            Latitude = 999,
            Longitude = -999,
            SpeedKmh = 500,
            FuelLevelPercent = 150,
            Progress = -1,
            DistanceRemainingM = -5,
            DistanceTravelledM = -10,
            FuelTankCapacity = 100
        };

        var service = CreateService();
        var method = typeof(VehicleTelemetryIngestionService).GetMethod(
            "NormalizeVehicleTelemetry",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        method.Invoke(service, new object[] { vehicle });

        vehicle.Latitude.Should().BeInRange(-90, 90);
        vehicle.Longitude.Should().BeInRange(-180, 180);
        vehicle.SpeedKmh.Should().BeLessThanOrEqualTo(220);
        vehicle.FuelLevelPercent.Should().BeInRange(0, 100);
        vehicle.Progress.Should().BeInRange(0, 1);
        vehicle.DistanceRemainingM.Should().BeGreaterThanOrEqualTo(0);
        vehicle.DistanceTravelledM.Should().BeGreaterThanOrEqualTo(0);
        vehicle.CurrentFuelLevel.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void GeneratePlaceholderPlate_HasExpectedLength()
    {
        var method = typeof(VehicleTelemetryIngestionService).GetMethod(
            "GeneratePlaceholderPlate",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var plate = (string)method.Invoke(null, Array.Empty<object>())!;
        plate.Should().StartWith("AUTO-");
        plate.Length.Should().BeLessThanOrEqualTo(15);
    }

    private static VehicleTelemetryIngestionService CreateService()
    {
        var opts = OptionsFactory.Create(new TelemetryIngestionOptions { Topic = "fleet/telemetry", Host = "localhost" });
        return new VehicleTelemetryIngestionService(
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<VehicleTelemetryIngestionService>.Instance,
            opts,
            Mock.Of<IHubContext<SmartFleet.Hubs.VehicleHub>>());
    }
}
