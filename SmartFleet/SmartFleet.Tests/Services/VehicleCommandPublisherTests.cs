using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SmartFleet.Options;
using SmartFleet.Services;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace SmartFleet.Tests.Services;

public class VehicleCommandPublisherTests
{
    [Fact]
    public async Task PublishRouteUpdateAsync_Throws_WhenPayloadIsNull()
    {
        var publisher = CreatePublisher(topic: "fleet/commands");

        await FluentActions.Awaiting(() => publisher.PublishRouteUpdateAsync(null!, CancellationToken.None))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task PublishRouteUpdateAsync_Throws_WhenStopsLessThanTwo(int stopCount)
    {
        var stops = new List<VehicleRouteCommandStop>();
        for (var i = 0; i < stopCount; i++)
        {
            stops.Add(new VehicleRouteCommandStop($"S{i}", 1 + i, 2 + i));
        }

        var payload = new VehicleRouteCommandPayload("veh-1", stops);
        var publisher = CreatePublisher(topic: "fleet/commands");

        await FluentActions.Awaiting(() => publisher.PublishRouteUpdateAsync(payload, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*at least two stops*");
    }

    [Fact]
    public async Task PublishRouteUpdateAsync_Throws_WhenVehicleIdMissing()
    {
        var payload = new VehicleRouteCommandPayload(string.Empty, new[]
        {
            new VehicleRouteCommandStop("A", 1, 1),
            new VehicleRouteCommandStop("B", 2, 2)
        });
        var publisher = CreatePublisher(topic: "fleet/commands");

        await FluentActions.Awaiting(() => publisher.PublishRouteUpdateAsync(payload, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Vehicle identifier*");
    }

    [Fact]
    public async Task PublishRouteUpdateAsync_Throws_WhenTopicNotConfigured()
    {
        var payload = new VehicleRouteCommandPayload("veh-1", new[]
        {
            new VehicleRouteCommandStop("A", 1, 1),
            new VehicleRouteCommandStop("B", 2, 2)
        });
        var publisher = CreatePublisher(topic: string.Empty);

        await FluentActions.Awaiting(() => publisher.PublishRouteUpdateAsync(payload, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*topic is not configured*");
    }

    [Fact]
    public async Task PublishRouteUpdateAsync_Throws_WhenCannotConnectToBroker()
    {
        var payload = new VehicleRouteCommandPayload("veh-1", new[]
        {
            new VehicleRouteCommandStop("A", 1, 1),
            new VehicleRouteCommandStop("B", 2, 2)
        });
        // Port 0 will fail fast; no fallback host added when primary is localhost
        var publisher = CreatePublisher(topic: "fleet/commands", host: "localhost", port: 0, fallbackHost: "localhost");

        await FluentActions.Awaiting(() => publisher.PublishRouteUpdateAsync(payload, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unable to connect*");
    }

    private static VehicleCommandPublisher CreatePublisher(
        string topic,
        string host = "localhost",
        int port = 1883,
        string? fallbackHost = null)
    {
        var options = OptionsFactory.Create(new VehicleCommandOptions
        {
            Topic = topic,
            Host = host,
            Port = port,
            FallbackHost = fallbackHost,
            KeepAliveSeconds = 10
        });

        return new VehicleCommandPublisher(options, NullLogger<VehicleCommandPublisher>.Instance);
    }
}
