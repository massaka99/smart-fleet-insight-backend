using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using SmartFleet.Options;

namespace SmartFleet.Telemetry;

public sealed class MqttTelemetryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttOptions _options;
    private readonly ITelemetryIngestionMonitor _monitor;
    private readonly ILogger<MqttTelemetryHostedService> _logger;
    private readonly Channel<string> _messageChannel;

    private IManagedMqttClient? _client;
    private Task? _processingTask;
    private Task? _inactivityTask;
    private int _reconnectAttempt;
    private CancellationToken _stoppingToken;

    public MqttTelemetryHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<MqttOptions> mqttOptions,
        ITelemetryIngestionMonitor monitor,
        ILogger<MqttTelemetryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = mqttOptions.Value;
        _monitor = monitor;
        _logger = logger;

        _messageChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(_options.ProcessingQueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("MQTT telemetry ingestion is disabled via configuration.");
            return;
        }

        _stoppingToken = stoppingToken;

        _processingTask = Task.Run(() => ProcessChannelAsync(_messageChannel.Reader, stoppingToken), CancellationToken.None);

        if (_options.InactivityWarningSeconds > 0)
        {
            _inactivityTask = Task.Run(() => MonitorInactivityAsync(stoppingToken), CancellationToken.None);
        }

        var factory = new MqttFactory();
        _client = factory.CreateManagedMqttClient();

        _client.ApplicationMessageReceivedAsync += args => HandleIncomingAsync(args, stoppingToken);
        _client.ConnectedAsync += OnConnectedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
        _client.ConnectingFailedAsync += OnConnectingFailedAsync;

        try
        {
            await _client.StartAsync(BuildManagedOptions());
            await _client.SubscribeAsync(_options.TelemetryTopic, MqttQualityOfServiceLevel.AtLeastOnce);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to start MQTT managed client.");
            throw;
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _messageChannel.Writer.TryComplete();

        if (_client is not null)
        {
            try
            {
                await _client.UnsubscribeAsync(_options.TelemetryTopic);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unsubscribe from telemetry topic during shutdown.");
            }

            try
            {
                await _client.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while stopping MQTT client during shutdown.");
            }
            finally
            {
                _client.Dispose();
            }
        }

        if (_processingTask is not null)
        {
            await Task.WhenAny(_processingTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
        }

        if (_inactivityTask is not null)
        {
            await Task.WhenAny(_inactivityTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
        }

        _monitor.ReportDisconnected("Service stopped.");

        await base.StopAsync(cancellationToken);
    }

    private ManagedMqttClientOptions BuildManagedOptions()
    {
        var clientId = $"backend-{Environment.MachineName}-{Guid.NewGuid():N}";
        var clientOptions = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(_options.Host, _options.Port)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(_options.KeepAliveSeconds))
            .WithTimeout(TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds))
            .WithWillTopic(_options.StatusTopic)
            .WithWillPayload(Encoding.UTF8.GetBytes(_options.OfflineWillPayload))
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithCleanSession();

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            clientOptions = clientOptions.WithCredentials(_options.Username, _options.Password);
        }

        if (_options.UseTls)
        {
            clientOptions = clientOptions.WithTlsOptions(o => o.UseTls());
        }

        return new ManagedMqttClientOptionsBuilder()
            .WithClientOptions(clientOptions.Build())
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(Math.Max(1, _options.ReconnectBaseSeconds)))
            .Build();
    }

    private Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        _reconnectAttempt = 0;
        _monitor.ReportConnected();
        _logger.LogInformation("Connected to MQTT broker {Host}:{Port} and subscribed to {Topic}.",
            _options.Host, _options.Port, _options.TelemetryTopic);
        return Task.CompletedTask;
    }

    private async Task OnConnectingFailedAsync(ConnectingFailedEventArgs args)
    {
        var reason = args.Exception?.Message ?? "unknown";
        var delay = CalculateBackoff();

        _logger.LogWarning(args.Exception, "MQTT connection attempt failed ({Reason}). Next retry in {Delay}.", reason, delay);

        try
        {
            await Task.Delay(delay, _stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation during shutdown.
        }
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        if (_stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var reason = args.Exception?.Message ?? args.Reason.ToString();
        _monitor.ReportDisconnected(reason);

        var delay = CalculateBackoff();
        _logger.LogWarning("Disconnected from MQTT broker: {Reason}. Next retry in {Delay}.", reason, delay);

        try
        {
            await Task.Delay(delay, _stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation during shutdown.
        }
    }

    private async Task HandleIncomingAsync(MqttApplicationMessageReceivedEventArgs args, CancellationToken stoppingToken)
    {
        if (args.ApplicationMessage is null)
        {
            return;
        }

        try
        {
            var payload = DecodePayload(args.ApplicationMessage);

            if (string.IsNullOrWhiteSpace(payload))
            {
                _logger.LogWarning("Received empty telemetry payload from topic {Topic}.", args.ApplicationMessage.Topic);
                return;
            }

            _monitor.ReportMessageReceived();
            await _messageChannel.Writer.WriteAsync(payload, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation during shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue telemetry payload for processing.");
        }
    }

    private async Task ProcessChannelAsync(ChannelReader<string> reader, CancellationToken stoppingToken)
    {
        try
        {
            while (await reader.WaitToReadAsync(stoppingToken))
            {
                while (reader.TryRead(out var payload))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<ITelemetryMessageProcessor>();
                    await processor.ProcessAsync(payload, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telemetry processing pipeline encountered a fatal error.");
        }
    }

    private async Task MonitorInactivityAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.InactivityWarningSeconds));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var snapshot = _monitor.GetSnapshot();

                if (snapshot.LastMessageUtc.HasValue)
                {
                    var elapsed = DateTime.UtcNow - snapshot.LastMessageUtc.Value;

                    if (elapsed.TotalSeconds >= _options.InactivityWarningSeconds)
                    {
                        _logger.LogWarning("No telemetry payload received for {ElapsedSeconds:F0} seconds.", elapsed.TotalSeconds);
                    }
                }
                else
                {
                    _logger.LogWarning("No telemetry payload has been received yet.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private TimeSpan CalculateBackoff()
    {
        var attempt = Interlocked.Increment(ref _reconnectAttempt);
        var baseDelay = Math.Max(1, _options.ReconnectBaseSeconds);
        var maxDelay = Math.Max(baseDelay, _options.ReconnectMaxSeconds);
        var exponential = Math.Min(maxDelay, baseDelay * Math.Pow(2, attempt - 1));

        return TimeSpan.FromSeconds(exponential);
    }

    private static string DecodePayload(MqttApplicationMessage message)
    {
        var segment = message.PayloadSegment;
        if (segment.Array is null || segment.Count == 0)
        {
            return string.Empty;
        }

        return Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
    }
}
